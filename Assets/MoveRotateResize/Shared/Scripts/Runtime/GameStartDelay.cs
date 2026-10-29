using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

namespace MoveRotateResize.Dynamic.General
{
    public class GameStartDelay : MonoBehaviour
    {
        public GameObject loadingScreen;
        public float blackScreenTime = 1f;

        private void Awake()
        {
            if (loadingScreen == null)
            {
                Debug.LogWarning("Loading screen is missing!");
                return;
            }

            loadingScreen.SetActive(true);

            if (loadingScreen.activeSelf)
            {
                StartCoroutine(DelayGameStart());
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (loadingScreen == null)
            {
                Debug.LogWarning("Loading screen is missing when loading scene: " + scene.name);
                return;
            }

            StartCoroutine(DelayGameStart());
        }

        private IEnumerator DelayGameStart()
        {
            loadingScreen.SetActive(true);
            yield return new WaitForSeconds(blackScreenTime);
            loadingScreen.SetActive(false);

            StartGame();
        }

        private void StartGame()
        {
            Debug.Log("Game has started!");
        }
    }
}