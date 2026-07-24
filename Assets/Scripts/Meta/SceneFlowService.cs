using UnityEngine.SceneManagement;

public static class SceneFlowService
{
    public static void ReloadActiveScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
