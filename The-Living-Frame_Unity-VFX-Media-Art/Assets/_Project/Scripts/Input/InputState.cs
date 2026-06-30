using UnityEngine;

namespace Project.Input
{
    public readonly struct InputState
    {
        public Vector3 AttractorPos { get; }
        public Vector2 FlowVector { get; }
        public float BurstEnergy { get; }
        public float WebcamMotionScore { get; }
        public bool WebcamActive { get; }
        public int MusicNoteIndex { get; }

        public InputState(
            Vector3 attractorPos,
            Vector2 flowVector,
            float burstEnergy,
            float webcamMotionScore,
            bool webcamActive,
            int musicNoteIndex = 0)
        {
            AttractorPos = attractorPos;
            FlowVector = flowVector;
            BurstEnergy = Mathf.Clamp01(burstEnergy);
            WebcamMotionScore = Mathf.Clamp01(webcamMotionScore);
            WebcamActive = webcamActive;
            MusicNoteIndex = musicNoteIndex;
        }

        public bool IsNeutral =>
            BurstEnergy < 0.01f &&
            WebcamMotionScore < 0.01f &&
            FlowVector.magnitude < 0.01f;

        public override string ToString() =>
            $"InputState(Attractor:{AttractorPos}, Flow:{FlowVector}, Burst:{BurstEnergy:F2}, Motion:{WebcamMotionScore:F2}, Note:{MusicNoteIndex})";
    }
}
