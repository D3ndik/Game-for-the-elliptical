using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Главное меню. Висит на объекте MainMenu в сцене MainMenu; интерфейс лежит в той же сцене (MainMenuUI),
// кнопки «Начать» и «Выйти» вызывают StartGame и Quit (назначено в инспекторе, On Click)
public class MainMenu : MonoBehaviour
{
    public string gameScene = "Arena"; // какую сцену загружать по кнопке «Начать»
    public Button startButton;         // выделяется при открытии меню, чтобы работали клавиатура и геймпад

    // При загрузке: на случай возврата из паузы снова запускаем время
    void Awake()
    {
        Time.timeScale = 1f;
    }

    // На старте: музыка меню и выделение кнопки «Начать»
    void Start()
    {
        SoundManager.PlayMusic(SoundManager.Music.Menu);
        if (startButton) startButton.Select();
    }

    // Кнопка «Начать»: загрузить арену
    public void StartGame() => SceneManager.LoadScene(gameScene);

    // Кнопка «Выйти»: в редакторе останавливает Play, в собранной игре закрывает её
    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
