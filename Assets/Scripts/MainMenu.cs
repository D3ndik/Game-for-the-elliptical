using UnityEngine;
using UnityEngine.SceneManagement;

// Главное меню: название игры, «Начать» и «Выйти»
public class MainMenu : MonoBehaviour
{
    public string gameScene = "Arena";

    void Awake()
    {
        Time.timeScale = 1f;
        UIKit.EnsureEventSystem();
        SoundManager.PlayMusic(SoundManager.Music.Menu);

        var canvas = UIKit.Canvas("MainMenuUI");
        UIKit.Stretch(UIKit.Rect("Background", canvas, new Color(0.07f, 0.08f, 0.09f), blocksClicks: true));

        var title = UIKit.Text(canvas, "Title", new Vector2(0.5f, 0.5f), new Vector2(0, 260), 140, TextAnchor.MiddleCenter);
        title.text = "ТАРАН";
        title.color = new Color(0.9f, 0.9f, 0.92f);

        var subtitle = UIKit.Text(canvas, "Subtitle", new Vector2(0.5f, 0.5f), new Vector2(0, 150), 32, TextAnchor.MiddleCenter);
        subtitle.text = "разгоняйся, тарань роботов и сталкивай их с арены";
        subtitle.color = new Color(0.65f, 0.68f, 0.7f);

        var start = UIKit.Button(canvas, "Начать", new Vector2(0, 0), () => SceneManager.LoadScene(gameScene));
        UIKit.Button(canvas, "Выйти", new Vector2(0, -110), UIKit.Quit);

        var hint = UIKit.Text(canvas, "Hint", new Vector2(0.5f, 0), new Vector2(0, 40), 24, TextAnchor.LowerCenter);
        hint.text = "WASD — разгон   SPACE — рывок   ESC — пауза";
        hint.color = new Color(1, 1, 1, 0.45f);

        start.Select(); // чтобы меню работало и с клавиатуры/геймпада
    }
}
