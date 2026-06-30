using System;
using UnityEngine;

public enum ThoughtEssenceCategory
{
    Fatigue,
    Anxiety,
    Sadness,
    Anger,
    Joy,
    Hope,
    Calm,
    Pressure
}

[Serializable]
public sealed class ThoughtEssence
{
    [SerializeField] private string label;
    [SerializeField] private ThoughtEssenceCategory category;
    [SerializeField] private Color color;
    [SerializeField, Range(0f, 1f)] private float intensity;

    public string Label => label;
    public ThoughtEssenceCategory Category => category;
    public Color Color => color;
    public float Intensity => intensity;

    public ThoughtEssence(string label, ThoughtEssenceCategory category, Color color, float intensity)
    {
        this.label = label;
        this.category = category;
        this.color = color;
        this.intensity = Mathf.Clamp01(intensity);
    }

    public ThoughtEssence Copy()
    {
        return new ThoughtEssence(label, category, color, intensity);
    }
}

public static class ThoughtEssenceCatalog
{
    private static readonly ThoughtEssence[] Defaults =
    {
        Create("졸리다", ThoughtEssenceCategory.Fatigue, 0.45f),
        Create("피곤함", ThoughtEssenceCategory.Fatigue, 0.58f),
        Create("무기력", ThoughtEssenceCategory.Fatigue, 0.72f),
        Create("번아웃", ThoughtEssenceCategory.Fatigue, 0.86f),
        Create("멍함", ThoughtEssenceCategory.Fatigue, 0.42f),
        Create("불안", ThoughtEssenceCategory.Anxiety, 0.78f),
        Create("초조함", ThoughtEssenceCategory.Anxiety, 0.72f),
        Create("걱정", ThoughtEssenceCategory.Anxiety, 0.66f),
        Create("두려움", ThoughtEssenceCategory.Anxiety, 0.8f),
        Create("긴장", ThoughtEssenceCategory.Anxiety, 0.62f),
        Create("우울함", ThoughtEssenceCategory.Sadness, 0.76f),
        Create("외로움", ThoughtEssenceCategory.Sadness, 0.68f),
        Create("그리움", ThoughtEssenceCategory.Sadness, 0.5f),
        Create("허전함", ThoughtEssenceCategory.Sadness, 0.58f),
        Create("상실감", ThoughtEssenceCategory.Sadness, 0.82f),
        Create("짜증", ThoughtEssenceCategory.Anger, 0.55f),
        Create("분노", ThoughtEssenceCategory.Anger, 0.82f),
        Create("억울함", ThoughtEssenceCategory.Anger, 0.7f),
        Create("답답함", ThoughtEssenceCategory.Anger, 0.66f),
        Create("예민함", ThoughtEssenceCategory.Anger, 0.52f),
        Create("설렘", ThoughtEssenceCategory.Joy, 0.56f),
        Create("기쁨", ThoughtEssenceCategory.Joy, 0.7f),
        Create("재미", ThoughtEssenceCategory.Joy, 0.46f),
        Create("뿌듯함", ThoughtEssenceCategory.Joy, 0.64f),
        Create("가벼움", ThoughtEssenceCategory.Joy, 0.5f),
        Create("기대", ThoughtEssenceCategory.Hope, 0.64f),
        Create("희망", ThoughtEssenceCategory.Hope, 0.76f),
        Create("용기", ThoughtEssenceCategory.Hope, 0.7f),
        Create("가능성", ThoughtEssenceCategory.Hope, 0.58f),
        Create("시작", ThoughtEssenceCategory.Hope, 0.48f),
        Create("평온", ThoughtEssenceCategory.Calm, 0.52f),
        Create("안정", ThoughtEssenceCategory.Calm, 0.64f),
        Create("휴식", ThoughtEssenceCategory.Calm, 0.5f),
        Create("괜찮음", ThoughtEssenceCategory.Calm, 0.44f),
        Create("숨고르기", ThoughtEssenceCategory.Calm, 0.4f),
        Create("부담", ThoughtEssenceCategory.Pressure, 0.68f),
        Create("압박감", ThoughtEssenceCategory.Pressure, 0.78f),
        Create("마감", ThoughtEssenceCategory.Pressure, 0.74f),
        Create("책임감", ThoughtEssenceCategory.Pressure, 0.62f),
        Create("완벽주의", ThoughtEssenceCategory.Pressure, 0.7f),
        Create("혼란", ThoughtEssenceCategory.Anxiety, 0.62f),
        Create("부끄러움", ThoughtEssenceCategory.Sadness, 0.48f),
        Create("질투", ThoughtEssenceCategory.Anger, 0.56f),
        Create("후회", ThoughtEssenceCategory.Sadness, 0.66f),
        Create("감사", ThoughtEssenceCategory.Joy, 0.7f),
        Create("호기심", ThoughtEssenceCategory.Hope, 0.48f),
        Create("집중", ThoughtEssenceCategory.Calm, 0.58f),
        Create("기다림", ThoughtEssenceCategory.Hope, 0.44f),
        Create("망설임", ThoughtEssenceCategory.Anxiety, 0.52f),
        Create("자신감", ThoughtEssenceCategory.Hope, 0.74f)
    };

    public static int Count => Defaults.Length;

    public static ThoughtEssence Get(int index)
    {
        if (Defaults.Length == 0)
        {
            return Create("감정", ThoughtEssenceCategory.Calm, 0.5f);
        }

        int safeIndex = Mathf.Abs(index) % Defaults.Length;
        return Defaults[safeIndex].Copy();
    }

    public static ThoughtEssence CreateCustom(string label, ThoughtEssenceCategory category, float intensity)
    {
        return Create(label, category, intensity);
    }

    private static ThoughtEssence Create(string label, ThoughtEssenceCategory category, float intensity)
    {
        return new ThoughtEssence(label, category, ColorForCategory(category, intensity), intensity);
    }

    private static Color ColorForCategory(ThoughtEssenceCategory category, float intensity)
    {
        Color baseColor;
        switch (category)
        {
            case ThoughtEssenceCategory.Fatigue:
                baseColor = new Color(0.45f, 0.7f, 1.0f, 0.42f);
                break;
            case ThoughtEssenceCategory.Anxiety:
                baseColor = new Color(0.62f, 0.45f, 1.0f, 0.46f);
                break;
            case ThoughtEssenceCategory.Sadness:
                baseColor = new Color(0.22f, 0.55f, 1.0f, 0.44f);
                break;
            case ThoughtEssenceCategory.Anger:
                baseColor = new Color(1.0f, 0.38f, 0.42f, 0.46f);
                break;
            case ThoughtEssenceCategory.Joy:
                baseColor = new Color(1.0f, 0.82f, 0.35f, 0.42f);
                break;
            case ThoughtEssenceCategory.Hope:
                baseColor = new Color(0.45f, 1.0f, 0.78f, 0.44f);
                break;
            case ThoughtEssenceCategory.Pressure:
                baseColor = new Color(1.0f, 0.58f, 0.28f, 0.46f);
                break;
            default:
                baseColor = new Color(0.55f, 0.95f, 1.0f, 0.42f);
                break;
        }

        float glow = Mathf.Lerp(0.82f, 1.18f, Mathf.Clamp01(intensity));
        return new Color(
            Mathf.Clamp01(baseColor.r * glow),
            Mathf.Clamp01(baseColor.g * glow),
            Mathf.Clamp01(baseColor.b * glow),
            baseColor.a
        );
    }
}
