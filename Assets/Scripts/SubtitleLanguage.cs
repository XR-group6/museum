using System;
using System.Collections.Generic;
using UnityEngine;

public static class SubtitleLanguage
{
    public static bool English { get; private set; }
    public static bool HasSelected { get; private set; }
    public static event Action Changed;
    static readonly Dictionary<string,string> translations = new Dictionary<string,string> {
        {"过去，渡海的人会在启程前，向妈祖祈求平安。", "Before setting sail, people prayed to Mazu for a safe crossing."},
        {"像这样的香炉，盛放的不只是香灰，也承载着人们对平安、团圆和归家的盼望。", "This incense burner holds more than ashes. It carries hopes for safety, reunion and a return home."},
        {"它随着人离开故土，又在海的另一边，陪伴一个家庭慢慢长大。", "It left its homeland with its owners and accompanied a family as it grew on the other side of the sea."},
        {"请用右手触摸并握住香炉。", "Touch the incense burner with your right controller and press the trigger."},
        {"欸，你可认得我?", "Ah, do you recognise me?"},
        {"或许，你愿意听听我渡海而来的故事?", "Would you like to hear the story of my journey across the sea?"},
        {"是否愿意进入香炉的记忆", "Enter the incense burner's memory?"},
        {"是", "Yes"},
        {"请妈祖保佑我们这次航海顺利。", "May Mazu bless us with a safe voyage."},
        {"爸爸，船快开了，我们该走了!", "Dad, the boat is about to leave. We should go!"},
        {"二十世纪四十年代末，许多人因局势动荡离开大陆、前往台湾。", "In the late 1940s, many people left mainland China for Taiwan amid political turmoil."},
        {"更早以前，来自福建、广东的渡海者已把妈祖信俗带到台湾;", "Earlier, people crossing from Fujian and Guangdong had brought Mazu traditions to Taiwan."},
        {"祈求海上平安，是其中一种心愿。", "One of their hopes was safety at sea."},
        {"爸爸，海上会不会突然刮大风啊??", "Dad, could a strong wind suddenly come up at sea?"},
        {"我又不会游泳。", "I can't swim."},
        {"小傻瓜，出发前我们不是求妈祖保佑过了吗?", "Oh, little one, didn't we pray to Mazu before we left?"},
        {"别怕，我们会平安到的。", "Don't be afraid. We'll arrive safely."},
        {"我也离开了自己的家，去往台湾。", "I too left my home and travelled to Taiwan."},
        {"出发之前，家人在香炉前点燃香火，祈求渡海平安。", "Before departure, the family lights incense and prays for a safe crossing."},
        {"家具、行李和日常用品，记录着一个家庭离开故乡前的准备。", "Furniture, luggage and everyday objects show a family's preparations to leave home."},
        {"本场景结合历史背景与艺术化重构。", "This scene combines historical context with artistic reconstruction."},
        {"请环顾四周，观察香炉、行李和家中的生活物件。", "Look around at the incense burner, luggage and everyday objects in the home."},
        {"可以使用左摇杆移动，右摇杆可以辅助您调整视角（请不要调整过快以免出现眩晕）", "Use the left stick to move and the right stick to turn. Turn slowly to avoid discomfort."}
    };
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { English=false; HasSelected=false; Changed=null; }
    public static void Select(bool english) { English=english; HasSelected=true; Changed?.Invoke(); }
    public static string Translate(string text)
    {
        if(!English || string.IsNullOrEmpty(text)) return text??"";
        var lines=text.Replace("\r\n","\n").Split('\n');
        for(int i=0;i<lines.Length;i++) { string translated; if(translations.TryGetValue(lines[i].Trim(),out translated)) lines[i]=translated; }
        return string.Join("\n",lines);
    }
}
