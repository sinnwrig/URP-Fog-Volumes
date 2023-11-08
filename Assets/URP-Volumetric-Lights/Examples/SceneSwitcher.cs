using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSwitcher : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod]
    static void InitSingleton()
    {
        GameObject go = new GameObject("SceneSwitcher");
        go.AddComponent<SceneSwitcher>();
        DontDestroyOnLoad(go);
    }


    void Update()
    {
        if (Input.anyKeyDown && SceneManager.sceneCountInBuildSettings > 0)
        {
            string input = Input.inputString;

            if (input.Length > 0 && int.TryParse(input[0].ToString(), out int res))
            {
                res--;
                res = Mathf.Clamp(res, 0, SceneManager.sceneCountInBuildSettings - 1);

                SceneManager.LoadScene(res);
            }
        }
    }
}
