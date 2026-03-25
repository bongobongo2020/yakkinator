namespace IndexTTSStudio.Models;

public class EmotionVector
{
    public float Joy { get; set; }
    public float Anger { get; set; }
    public float Sadness { get; set; }
    public float Fear { get; set; }
    public float Disgust { get; set; }
    public float Melancholy { get; set; }
    public float Surprise { get; set; }
    public float Calm { get; set; }

    public float[] ToArray() => [Joy, Anger, Sadness, Fear, Disgust, Melancholy, Surprise, Calm];
}
