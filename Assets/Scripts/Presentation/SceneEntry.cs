using UnityEngine;

namespace ChromeDino.Presentation
{
    public enum SceneRole { MainMenu, Training, AITest }

    public sealed class SceneEntry : MonoBehaviour
    {
        [SerializeField] private SceneRole role;

        private void Start()
        {
            Application.targetFrameRate = 60;
            if (role == SceneRole.Training) gameObject.AddComponent<TrainingScreen>();
            else if (role == SceneRole.AITest) gameObject.AddComponent<AITestScreen>();
            else gameObject.AddComponent<MainMenuScreen>();
        }
    }
}
