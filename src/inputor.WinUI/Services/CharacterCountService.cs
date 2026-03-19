namespace Inputor.App.Services;

public static class CharacterCountService
{
    public static int CountSupportedCharacters(string text)
    {
        var count = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (IsChineseRune(rune.Value) || IsEnglishLetter(rune.Value))
            {
                count++;
            }
        }

        return count;
    }

    public static int CountChineseCharacters(string text)
    {
        var count = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (IsChineseRune(rune.Value))
            {
                count++;
            }
        }

        return count;
    }

    public static int CountEnglishLetters(string text)
    {
        var count = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (IsEnglishLetter(rune.Value))
            {
                count++;
            }
        }

        return count;
    }

    public static bool IsChineseRune(int value)
    {
        return (value >= 0x3400 && value <= 0x4DBF)
            || (value >= 0x4E00 && value <= 0x9FFF)
            || (value >= 0xF900 && value <= 0xFAFF)
            || (value >= 0x20000 && value <= 0x2A6DF)
            || (value >= 0x2A700 && value <= 0x2EBEF)
            || (value >= 0x30000 && value <= 0x323AF);
    }

    public static bool IsEnglishLetter(int value)
    {
        return (value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z');
    }
}
