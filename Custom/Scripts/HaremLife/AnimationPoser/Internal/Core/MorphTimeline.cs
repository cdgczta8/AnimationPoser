using UnityEngine;
using UnityEngine.Events;
using System;
using System.Linq;
using System.Collections.Generic;
using SimpleJSON;

namespace HaremLife
{
	public partial class AnimationPoser : MVRScript
	{
		private class MorphTimeline : Timeline {
			public MorphCapture myMorphCapture;

			public MorphTimeline(MorphCapture morphCapture) {
				myMorphCapture = morphCapture;
			}

			public void SetEndpoints(float startEntry,
									 float endEntry) {

				if(myKeyframes.FirstOrDefault(k=>k.myIsFirst) == null) {
					AddKeyframe(new MorphKeyframe("first", startEntry));
				}

				if(myKeyframes.FirstOrDefault(k=>k.myIsLast) == null) {
					AddKeyframe(new MorphKeyframe("last", endEntry));
				}

				ComputeControlPoints();
			}

			public override void CaptureKeyframe(float time) {
				float entry = myMorphCapture.myMorph.morphValue;
				AddKeyframe(new MorphKeyframe(time, entry));
			}

			public override void UpdateKeyframe(Keyframe keyframe) {
				RemoveKeyframe(keyframe);
				CaptureKeyframe(keyframe.myTime);
			}

			public override void ComputeControlPoints() {
				List<float> ts = new List<float>();
				List<float> vs = new List<float>();
				List<Keyframe> keyframes = new List<Keyframe>(myKeyframes.OrderBy(k => k.myTime));
				if(keyframes.Count < 3)
					return;

				for(int i=0; i<keyframes.Count; i++) {
					MorphKeyframe morphKeyframe = keyframes[i] as MorphKeyframe;
					ts.Add(morphKeyframe.myTime);
					vs.Add(morphKeyframe.myMorphEntry);
				}

				List<ControlPoint> controlPoints = AutoComputeControlPoints(vs, ts);

				for(int i=0; i<keyframes.Count; i++) {
					MorphKeyframe morphKeyframe = keyframes[i] as MorphKeyframe;
					morphKeyframe.myControlPointIn = controlPoints[i].In;
					morphKeyframe.myControlPointOut = controlPoints[i].Out;
				}
			}

			public void UpdateCurve(float t)
			{
				if (!myMorphCapture.myApply)
					return;

                int n = BinarySearch(t);
				MorphKeyframe k1 = myKeyframes[n] as MorphKeyframe;
				MorphKeyframe k2 = myKeyframes[n+1] as MorphKeyframe;

				t = (t-k1.myTime)/(k2.myTime-k1.myTime);

				float c1 = k1.myMorphEntry;
				float c2 = k1.myControlPointOut;
				float c3 = k2.myControlPointIn;
				float c4 = k2.myMorphEntry;

				myMorphCapture.myMorph.morphValue = EvalBezier(t, c1, c2, c3, c4);
			}

			public void Merge(Timeline timeline2, float transition1Duration, float transition2Duration) {
				float totalDuration = transition1Duration + transition2Duration;

				for (int i=0; i<myKeyframes.Count; ++i) {
					myKeyframes[i].myTime = myKeyframes[i].myTime * transition1Duration/totalDuration;
				}

				for (int i=1; i<timeline2.myKeyframes.Count; ++i) {
					timeline2.myKeyframes[i].myTime = transition1Duration/totalDuration
										+ timeline2.myKeyframes[i].myTime * transition2Duration/totalDuration;
					myKeyframes.Add(timeline2.myKeyframes[i]);
				}

				myKeyframes.Remove(myKeyframes[0]);
				myKeyframes.Remove(myKeyframes[myKeyframes.Count-1]);
			}
		}
    }
}