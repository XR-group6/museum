using System;
using System.Collections.Generic;
using UnityEngine;

public static class SubtitleLanguage
{
    public static bool English { get; private set; }
    public static bool HasSelected { get; private set; }
    public static bool ReturnedToMuseum { get; private set; }
    public static void CompleteJourney() { ReturnedToMuseum = true; }
    public static event Action Changed;
    static readonly Dictionary<string,string> translations = new Dictionary<string,string> {
        {"女儿：爸，怎么把它也带来了？", "Daughter: Dad, why did you bring that too?"},
        {"父亲：等回去那天，还要求妈祖保佑航海顺利呢。", "Father: When we go back, we will still ask Mazu to bless our voyage."},
        {"快去上学吧。", "Go on, off to school."},
        {"父亲：快去上学吧。", "Father: Go on, off to school."},
        {"香炉：等他们再求航海顺利，我就能回家。那我就等。", "Incense burner: When they pray for another safe voyage, I can go home. So I will wait."},
        {"女儿：爸、妈，我上班去了。", "Daughter: Dad, Mum, I am off to work."},
        {"父亲：路上小心。", "Father: Take care on the way."},
        {"香炉：我还在等。", "Incense burner: I am still waiting."},
        {"女儿：爸，尝尝我做的鱼。", "Daughter: Dad, try the fish I made."},
        {"父亲：好，你也趁热吃。", "Father: All right. You eat while it is hot too."},
        {"香炉：这些年，我又听见了许多心愿。", "Incense burner: Over the years, I have heard many more wishes."},
        {"我还在等。", "I am still waiting."},
        {"父亲：不知道那边的老屋，还在不在。", "Father: I wonder if the old house back there is still standing."},
        {"约1951年｜清晨", "Around 1951 | Morning"},
        {"许多人渡海来到台湾，仍盼着早日回乡。", "Many people crossed the sea to Taiwan, still hoping to return home soon."},
        {"一家人在港口边安顿下来，也渐渐适应了这里的生活。", "The family settled near the harbour and gradually adjusted to life here."},
        {"约1960年｜白天", "Around 1960 | Daytime"},
        {"两岸相隔，返乡的日子一直定不下来。", "Separated by the strait, they could not say when they would return home."},
        {"当年牵着父亲的手上船的女孩，已经在台湾长大，开始工作了。", "The girl who once boarded the boat holding her father's hand has grown up in Taiwan and started work."},
        {"约1970年｜除夕夜", "Around 1970 | Lunar New Year's Eve"},
        {"在台湾，除夕一家人同桌吃年夜饭，叫作“围炉”。", "In Taiwan, sharing a family meal on Lunar New Year's Eve is called gathering around the hearth."},
        {"如今，女儿也带着自己的孩子回来过年，", "Now the daughter brings her own child home for the New Year,"},
        {"一张桌旁坐了三代人。", "and three generations sit around one table."},
        {"约1980年｜黄昏", "Around 1980 | Dusk"},
        {"那时，两岸还未开放探亲。父亲惦记着海那边的老屋，", "Cross-strait family visits were not yet permitted. Father thought of the old house across the sea,"},
        {"也在海这边的屋檐下，看着女儿和外孙两代人长大。", "while watching his daughter and grandchild grow up under this roof."},
        {"1988年｜返乡探亲", "1988 | Returning Home to Visit Family"},
        {"两岸开放探亲后，许多离乡多年的人终于有机会回去看看。", "After cross-strait family visits were allowed, many who had been away for years finally had a chance to return."},
        {"离开故乡近四十年，父亲也等到了自己的归期。", "After almost forty years away, Father too could finally go home."},
        {"父亲：请妈祖保佑我们这次航海顺利。", "Father: May Mazu bless us with a safe voyage."},
        {"香炉：那我……", "Incense burner: Then I..."},
        {"父亲：这次，我就不带你了。", "Father: This time, I will leave you here."},
        {"父亲：当年我把你带过来，现在这里也是你的家了。", "Father: I brought you here all those years ago. This is your home now too."},
        {"香炉：看来你终于要回家了。我也知道，你还会回家。", "Incense burner: It seems you are finally going home. And I know you will come home again."},
        {"现在，你可以在屋里自由走动，看看桌上留下的证件。", "You can now walk freely around the room and look at the documents left on the table."},
        {"可以使用右摇杆跟随灯光转动视角", "Use the right stick to turn your view and follow the lights."},
        {"请用右手触碰并握住香炉，继续我们的旅程。", "Touch the incense burner with your right controller and press the trigger to continue our journey."},
        {"请向前走到香炉前，用右手触碰握住香炉。", "Move forward to the incense burner. Touch it with your right controller and press the trigger."},
        {"约1951年：新的生活", "Around 1951: A New Life"},
        {"约1960年：女儿长大了", "Around 1960: The Daughter Grows Up"},
        {"约1970年：团圆饭", "Around 1970: The Reunion Dinner"},
        {"约1980年：等待与思念", "Around 1980: Waiting and Longing"},
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
        {"可以使用左摇杆移动，右摇杆可以辅助您调整视角（请不要调整过快以免出现眩晕）。试试用右摇杆的激光指向桌上的台胞证并按下扳机。", "Use the left stick to move and the right stick to turn. Turn slowly to avoid discomfort. Try aiming your right controller's laser at the Taiwan Compatriot Permit on the table, then press the trigger."}
    };
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { English=false; HasSelected=false; ReturnedToMuseum=false; Changed=null; }
    public static void Select(bool english) { English=english; HasSelected=true; Changed?.Invoke(); }
    public static void BeginNewVisit()
    {
        English = false;
        HasSelected = false;
        ReturnedToMuseum = false;
        Changed?.Invoke();
    }
    public static string Translate(string text)
    {
        if(!English || string.IsNullOrEmpty(text)) return text??"";
        var lines=text.Replace("\r\n","\n").Split('\n');
        for(int i=0;i<lines.Length;i++) { string translated; if(translations.TryGetValue(lines[i].Trim(),out translated)) lines[i]=translated; }
        return string.Join("\n",lines);
    }
}
