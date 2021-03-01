using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System;
using System.Text;
using System.Collections.Generic;

namespace MacGruber
{
	public partial class IdlePoser : MVRScript
	{
		private StringBuilder myPlayInfoBuilder;

		private bool myDebugCurvesActive = false;

		private Mesh myDebugLineMesh;
		private Mesh myDebugSphereMesh;
		private Material myDebugLineMaterial;
		private List<Vector3> myDebugLineVertices;
		private List<Color32> myDebugLineColors;
		private Color32[] myDebugLineColorsBuffer;
		private List<int> myDebugLineIndices;
		private int[] myDebugLineIndicesBuffer;
		private List<State> myDebugTransition;
		private HashSet<ulong> myDebugPathHashes;
		private HashSet<uint> myDebugTransitionHashes;
		private Vector3[] myDebugLinePositions;
		private Vector3[] myDebugLineTempA;
		private Vector3[] myDebugLineTempB;

		private Material myDebugStateSphereMaterialPrefab;
		private List<Material> myDebugStateSphereMaterials;
		private const float DEBUG_CUBE_REGULAR_SIZE = 0.010f;
		private const float DEBUG_CUBE_CONTROL_SIZE = 0.006f;
		private static readonly Vector3 DEBUG_CUBE_REGULAR_VECTOR = new Vector3(DEBUG_CUBE_REGULAR_SIZE, DEBUG_CUBE_REGULAR_SIZE, DEBUG_CUBE_REGULAR_SIZE);
		private static readonly Vector3 DEBUG_CUBE_CONTROL_VECTOR = new Vector3(DEBUG_CUBE_CONTROL_SIZE, DEBUG_CUBE_CONTROL_SIZE, DEBUG_CUBE_CONTROL_SIZE);
		private static readonly Color32 DEBUG_CONTROL_COLOR = new Color32(128,128,128,255); // grey
		private static readonly Color32 DEBUG_TRANSITION_COLOR = new Color32(128,128,128,255); // grey
		private static readonly Color32 DEBUG_TRANSITION_ONEWAY_COLOR = new Color32(170,20,20,255); // grey red

		private void DebugSwitchShowCurves(bool enabled)
		{
			enabled = myDebugShowPaths.val || myDebugShowTransitions.val;

			if (enabled && !myDebugCurvesActive)
				InitDebugCurves();
			else if (!enabled && myDebugCurvesActive)
				DestroyDebugCurves();
		}

		private void InitDebugCurves()
		{
			// lines
			Shader lineShader = Shader.Find("Battlehub/RTHandles/VertexColor");
			myDebugLineMaterial = new Material(lineShader);
			myDebugLineMesh = new Mesh();
			myDebugLineMesh.MarkDynamic();

			myDebugLineVertices = new List<Vector3>();
			myDebugLineColors = new List<Color32>();
			myDebugLineIndices = new List<int>();
			myDebugTransition = new List<State>(MAX_STATES);
			myDebugPathHashes = new HashSet<ulong>();
			myDebugTransitionHashes = new HashSet<uint>();
			myDebugLinePositions = new Vector3[MAX_STATES];
			myDebugLineTempA = new Vector3[MAX_STATES-1];
			myDebugLineTempB = new Vector3[MAX_STATES-2];

			// spheres
			GameObject spherePrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			myDebugSphereMesh = spherePrefab.GetComponent<MeshFilter>().mesh;
			DestroyImmediate(spherePrefab);

			Shader[] shaders = Resources.FindObjectsOfTypeAll<Shader>();
			Shader sphereShader = Array.Find(shaders, s => s.name == "Custom/TransparentHUD");
			myDebugStateSphereMaterialPrefab = new Material(sphereShader);
			myDebugStateSphereMaterialPrefab.color = Color.white;
			myDebugStateSphereMaterialPrefab.renderQueue = 3006;
			
			myDebugStateSphereMaterials = new List<Material>();
			myDebugCurvesActive = true;
		}		

		private void DestroyDebugCurves()
		{		
			if (!myDebugCurvesActive)
				return;
			myDebugCurvesActive = false;

			Destroy(myDebugLineMesh);
			Destroy(myDebugLineMaterial);
			myDebugLineMesh = null;
			myDebugLineMaterial = null;
			myDebugLineVertices = null;
			myDebugLineColors = null;
			myDebugLineColorsBuffer = null;
			myDebugLineIndices = null;
			myDebugLineIndicesBuffer = null;
			myDebugTransition = null;
			myDebugPathHashes = null;
			myDebugTransitionHashes = null;
			myDebugLinePositions = null;
			myDebugLineTempA = null;
			myDebugLineTempB = null;

			for (int i=0; i<myDebugStateSphereMaterials.Count; ++i)
				Destroy(myDebugStateSphereMaterials[i]);
			myDebugStateSphereMaterials = null;
			Destroy(myDebugStateSphereMaterialPrefab);
			myDebugStateSphereMaterialPrefab = null;
		}

		private void DebugUpdateUI()
		{
			if (myDebugCurvesActive)
			{
				if (!myPaused || myNextState != null || myTransition.Count > 0 || myPlayMode)
				{
					foreach (var s in myStates)
					{
						State state = s.Value;
						if (state == myCurrentState || myCurrentTransition.Contains(state))
							continue; // these have been already updated
						for (int i=0; i<myControlCaptures.Count; ++i)
							myControlCaptures[i].UpdateState(state);
					}
				}
				
				DebugUpdateLines();
				DebugUpdateSpheres();
			}

			if (myDebugShowInfo.val)
			{				
				if (myPlayInfoBuilder == null)
					myPlayInfoBuilder = new StringBuilder();
				else
					myPlayInfoBuilder.Length = 0;

				if (myNextState != null)
				{
					myPlayInfoBuilder.Append("Current Transition:\n  ");
					myPlayInfoBuilder.AppendFormat("{0:N2}", myClock).Append(" / ").AppendFormat("{0:N2}", myDuration);
					myPlayInfoBuilder.Append("\n  ").Append(myCurrentTransition[0].myName);
					for (int i=1; i<myCurrentTransition.Count; ++i)
						myPlayInfoBuilder.Append("\n  => ").Append(myCurrentTransition[i].myName);
				}
				else
				{
					myPlayInfoBuilder.Append("Current State:\n  ");
					if (myCurrentState != null)
					{
						if (myCurrentState.myWaitInfiniteDuration)
							myPlayInfoBuilder.AppendFormat("Infinite");
						else if (myClock >= myDuration && myCurrentState.myWaitForSync)
							myPlayInfoBuilder.AppendFormat("Waiting for TriggerSync");
						else
							myPlayInfoBuilder.AppendFormat("{0:N2}", myClock).Append(" / ").AppendFormat("{0:N2}", myDuration);
						myPlayInfoBuilder.Append("\n  ");
						myPlayInfoBuilder.Append(myCurrentState.myName);
					}
					else
					{
						myPlayInfoBuilder.Append("NULL");
					}
				}
				
				myPlayInfoBuilder.Append("\n\n");
				
				if (myStateMask == 0)
				{
					myPlayInfoBuilder.Append("StateMask:\n  Clear");
				}
				else
				{
					myPlayInfoBuilder.Append("StateMask:\n  ");
					for (int i=0; i<8; ++i)
					{
						if ((myStateMask & (1u << (i+1))) != 0)
							myPlayInfoBuilder.Append((char)('A' + i));
					}
				}

				myPlayInfo.val = myPlayInfoBuilder.ToString();
			}
		}

		private void DebugUpdateSpheres()
		{
			// update sphere positions
			int sphereIndex = 0;
			Matrix4x4 matrix = Matrix4x4.identity;
			foreach (var s in myStates)
			{
				State state = s.Value;
				int stateType = state.myStateType;
				if (stateType >= NUM_STATETYPES)
					continue;

				foreach (var e in state.myControlEntries)
				{
					ControlEntryAnchored ce = e.Value;
					Material material;
					if (sphereIndex >= myDebugStateSphereMaterials.Count)
						myDebugStateSphereMaterials.Add(material = new Material(myDebugStateSphereMaterialPrefab));
					else
						material = myDebugStateSphereMaterials[sphereIndex];
					sphereIndex++;

					material.color = DebugGetStateColor(state);
					Vector3 position = ce.myEntry.myPosition;
					Vector3 localScale = state.IsControlPoint ? DEBUG_CUBE_CONTROL_VECTOR : DEBUG_CUBE_REGULAR_VECTOR;
					matrix.SetTRS(position, Quaternion.identity, localScale);					
					Graphics.DrawMesh(myDebugSphereMesh, matrix, material, gameObject.layer, null, 0, null, castShadows: false, receiveShadows: false);
				}
			}

			// cleanup unused materials
			if (sphereIndex < myDebugStateSphereMaterials.Count)
			{
				for (int i=sphereIndex; i<myDebugStateSphereMaterials.Count; ++i)
					Destroy(myDebugStateSphereMaterials[i]);
				myDebugStateSphereMaterials.RemoveRange(sphereIndex, myDebugStateSphereMaterials.Count-sphereIndex);
			}
		}

		private void DebugUpdateLines()
		{
			myDebugLineVertices.Clear();
			myDebugLineColors.Clear();
			myDebugLineIndices.Clear();
			myDebugPathHashes.Clear();
			myDebugTransitionHashes.Clear();
						
			uint index = 0;
			foreach (var s in myStates)
				s.Value.myDebugIndex = index++;
				
			if (myDebugShowTransitions.val)
			{
				if (myDebugShowSelectedOnly.val)
				{
					State source;
					if (myStates.TryGetValue(myMainState.val, out source) && source != null)
						DebugGatherTransitionsForState(source);
				}
				else
				{
					foreach (var s in myStates)
						DebugGatherTransitionsForState(s.Value);
				}
			}
			
			foreach (var s in myStates)
			{
				State state = s.Value;
				if (state.IsControlPoint)
					continue;
				myDebugTransition.Add(state);
				DebugGather();
				myDebugTransition.Clear();
			}

			if (myDebugLineVertices.Count == 0)
			{
				myDebugLineMesh.Clear();
			}
			else
			{
				if (myDebugLineVertices.Count != myDebugLineMesh.vertexCount)
					myDebugLineMesh.Clear(true);
				myDebugLineMesh.SetVertices(myDebugLineVertices);

				if (myDebugLineColorsBuffer == null || myDebugLineColorsBuffer.Length != myDebugLineColors.Count)
					myDebugLineColorsBuffer = new Color32[myDebugLineColors.Count];
				for (int i=0; i<myDebugLineColors.Count; ++i)
					myDebugLineColorsBuffer[i] = myDebugLineColors[i];
				myDebugLineMesh.colors32 = myDebugLineColorsBuffer;

				if (myDebugLineIndicesBuffer == null || myDebugLineIndicesBuffer.Length != myDebugLineIndices.Count)
					myDebugLineIndicesBuffer = new int[myDebugLineIndices.Count];
				for (int i=0; i<myDebugLineIndices.Count; ++i)
					myDebugLineIndicesBuffer[i] = myDebugLineIndices[i];
				myDebugLineMesh.SetIndices(myDebugLineIndicesBuffer, MeshTopology.Lines, 0);

				Graphics.DrawMesh(myDebugLineMesh, Matrix4x4.identity, myDebugLineMaterial, gameObject.layer, null, 0, null, castShadows: false, receiveShadows: false);
			}
		}

		private void DebugGather()
		{
			State source = myDebugTransition[0];
			State current = myDebugTransition[myDebugTransition.Count-1];
			State selectedState = UIGetState();
			for (int i=0; i<current.myTransitions.Count; ++i)
			{
				State next = current.myTransitions[i];
				if (myDebugTransition.Contains(next))
					continue;

				if (next.IsControlPoint)
				{
					if (myDebugTransition.Count >= MAX_STATES-1)
						continue;
					myDebugTransition.Add(next);
					DebugGather();
					myDebugTransition.RemoveAt(myDebugTransition.Count-1);
				}
				else
				{
					if (next.IsRegularState && !DoAcceptRegularState(source, next, true))
						continue;
						
					if (myDebugShowSelectedOnly.val && next != selectedState)
					{
						bool foundSelected = false;
						for (int t=0; t <myDebugTransition.Count; ++t)
							foundSelected |= myDebugTransition[t] == selectedState;
						if (!foundSelected)
							continue;
					}

					myDebugTransition.Add(next);
					DebugGatherPath();
					DebugGatherTransitions();
					myDebugTransition.RemoveAt(myDebugTransition.Count-1);
				}
			}
		}

		private void DebugGatherPath()
		{
			if (!myDebugShowPaths.val)
				return;
			
			ulong hash = 0;
			int entryCount = myDebugTransition.Count;
			if (myDebugTransition[0].myDebugIndex < myDebugTransition[entryCount-1].myDebugIndex)
			{
				for (int i=0; i<entryCount; ++i)
					hash = (hash << 10) | (myDebugTransition[i].myDebugIndex & 0x3FF);
			}
			else
			{
				for (int i=entryCount-1; i>=0; --i)
					hash = (hash << 10) | (myDebugTransition[i].myDebugIndex & 0x3FF);
			}
			
			if (!myDebugPathHashes.Add(hash))
				return;
				

			int numSamples = DISTANCE_SAMPLES[entryCount];
			int numLines = numSamples + 1;
			float numLinesF = (float)numLines;
			for (int i=0; i<myControlCaptures.Count; ++i)
			{
				ControlCapture cc = myControlCaptures[i];
				for (int j=0; j<entryCount; ++j)
				{
					ControlEntryAnchored ce;
					if (!myDebugTransition[j].myControlEntries.TryGetValue(cc, out ce))
						continue;
					myDebugLinePositions[j] = ce.myEntry.myPosition;
				}

				Color32 startColor = DebugGetStateColor(myDebugTransition[0]);
				Color32 endColor = DebugGetStateColor(myDebugTransition[entryCount-1]);

				int startVertex = myDebugLineVertices.Count;
				myDebugLineVertices.Add(myDebugLinePositions[0]);
				myDebugLineColors.Add(startColor);
				switch (entryCount)
				{
					case 4:
					{
						// evaluating cubic Bézier curve using Bernstein polynomials
						for (int j=1; j<=numSamples; ++j)
						{
							float t = j / numLinesF;
							float s = 1.0f - t;
							float t2 = t*t;
							float s2 = s*s;
							Vector3 vertex = (s*s2)      * myDebugLinePositions[0]
								           + (3.0f*s2*t) * myDebugLinePositions[1]
								           + (3.0f*s*t2) * myDebugLinePositions[2]
								           +      (t*t2) * myDebugLinePositions[3];
							myDebugLineVertices.Add(vertex);
							myDebugLineColors.Add(Color32.LerpUnclamped(startColor, endColor, t));
						}
						break;
					}
					case 3:
					{
						// evaluating quadratic Bézier curve using Bernstein polynomials
						for (int j=1; j<=numSamples; ++j)
						{
							float t = j / numLinesF;
							float s = 1.0f - t;
							Vector3 vertex = (s*s)      * myDebugLinePositions[0]
							               + (2.0f*s*t) * myDebugLinePositions[1]
							               +      (t*t) * myDebugLinePositions[2];
							myDebugLineVertices.Add(vertex);
							myDebugLineColors.Add(Color32.LerpUnclamped(startColor, endColor, t));
						}
						break;
					}
					default:
					{
						// linear Bézier curve, no need for sample points
						break;
					}
				}
				myDebugLineVertices.Add(myDebugLinePositions[entryCount-1]);
				myDebugLineColors.Add(endColor);

				int endVertex = startVertex+numLines;
				for (int j=startVertex; j<endVertex; ++j)
				{
					myDebugLineIndices.Add(j);
					myDebugLineIndices.Add(j+1);
				}
			}
		}

		private void DebugGatherTransitions()
		{
			if (!myDebugShowTransitions.val || !myDebugShowSelectedOnly.val)
				return;
			
			for (int t=0; t<myDebugTransition.Count-1; ++t)
			{		
				State source = myDebugTransition[t];
				State target = myDebugTransition[t+1];
				DebugGatherTransition(source, target);
			}
		}
		
		private void DebugGatherTransitionsForState(State source)
		{
			if (source == null)
				return;
				
			for (int i=0; i<source.myTransitions.Count; ++i)
			{
				State target = source.myTransitions[i];
				DebugGatherTransition(source, target);
			}
		}
		
		private void DebugGatherTransition(State source, State target)
		{
			uint hash = source.myDebugIndex & 0x3FF;
			uint hash2 = target.myDebugIndex & 0x3FF;
			if (source.myDebugIndex <= target.myDebugIndex)
				hash = (hash << 10) | hash2;
			else
				hash = (hash2 << 10) | hash;
				
			if (!myDebugTransitionHashes.Add(hash))
				return;
				
			bool oneWay = !target.myTransitions.Contains(source);				
			Color32 color = oneWay ? DEBUG_TRANSITION_ONEWAY_COLOR : DEBUG_TRANSITION_COLOR;

			for (int j=0; j<myControlCaptures.Count; ++j)
			{
				ControlCapture cc = myControlCaptures[j];
				ControlEntryAnchored ceSource;
				ControlEntryAnchored ceTarget;
				if (!source.myControlEntries.TryGetValue(cc, out ceSource))
					continue;
				if (!target.myControlEntries.TryGetValue(cc, out ceTarget))
					continue;

				int vertex = myDebugLineVertices.Count;
				myDebugLineVertices.Add(ceSource.myEntry.myPosition);
				myDebugLineVertices.Add(ceTarget.myEntry.myPosition);
				myDebugLineColors.Add(color);
				myDebugLineColors.Add(DEBUG_TRANSITION_COLOR);
				myDebugLineIndices.Add(vertex);
				myDebugLineIndices.Add(vertex+1);
			}
		}

		private static Color32 DebugGetStateColor(State state)
		{
			if (state.IsRegularState)
			{
				// 'Distinct Colors' by Sasha Trubetskoy
				// https://sashamaps.net/docs/tools/20-colors/
				switch (state.myStateGroup)
				{
					case  1: return new Color32(230, 25, 75,255); // red
					case  2: return new Color32( 60,180, 75,255); // green
					case  3: return new Color32(255,225, 25,255); // yellow
					case  4: return new Color32(240, 50,230,255); // magenta
					case  5: return new Color32( 70,240,240,255); // cyan
					case  6: return new Color32(245,130, 48,255); // orange
					case  7: return new Color32(  0,128,128,255); // teal
					case  8: return new Color32(170,110, 40,255); // brown
					case  9: return new Color32(191,239, 69,255); // lime
					case 10: return new Color32(145, 30,180,255); // purple
					case 11: return new Color32(  0,  0,117,255); // navy
					case 12: return new Color32(128,128,  0,255); // olive
					default: return new Color32(  0,130,200,255); // blue
				}
			}
			else
			{
				return DEBUG_CONTROL_COLOR;
			}
		}
	}
}