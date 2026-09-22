using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace EnCare
{
    public sealed class HandoverCutscene : MonoBehaviour
    {
        [SerializeField] PlayableDirector director;
        [Tooltip("Optional root containing the NPC and animated duplicate basket.")]
        [SerializeField] GameObject cutsceneRoot;
        [Tooltip("Basket, controller visuals/interactors. NEVER put XR Origin, XR camera, or this object here.")]
        [SerializeField] GameObject[] hideDuringCutscene = new GameObject[0];
        [Tooltip("Locomotion providers only. Keep head tracking and XR camera enabled.")]
        [SerializeField] Behaviour[] disableDuringCutscene = new Behaviour[0];
        public UnityEvent onCutsceneFinished = new UnityEvent();
        bool started;

        void OnEnable() { if (director != null) director.stopped += OnStopped; }
        void OnDisable() { if (director != null) director.stopped -= OnStopped; }

        public void PlayHandover()
        {
            if (started) return;
            started = true;
            if (director == null || director.playableAsset == null)
            {
                Debug.LogError("EnCare: collection completed, but assign a PlayableDirector and Timeline for handover.", this);
                return;
            }
            StartCoroutine(BeginNextFrame());
        }
        IEnumerator BeginNextFrame()
        {
            // Let the last deposit and XRI update finish before hiding the gameplay basket.
            yield return null;
            foreach (Behaviour behaviour in disableDuringCutscene)
                if (behaviour != null) behaviour.enabled = false;
            foreach (GameObject obj in hideDuringCutscene)
                if (obj != null) obj.SetActive(false);
            if (cutsceneRoot != null) cutsceneRoot.SetActive(true);
            director.extrapolationMode = DirectorWrapMode.None;
            director.time = 0;
            director.Play();
        }
        void OnStopped(PlayableDirector stoppedDirector)
        {
            if (started) onCutsceneFinished.Invoke();
        }
        // Wire a retry button's OnClick to this method. Add the scene to Build Profiles.
        public void RestartLevel() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
