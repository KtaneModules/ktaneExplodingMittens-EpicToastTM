using UnityEngine;
using System.Linq;
using KModkit;
using System.Collections;
using Random = UnityEngine.Random;
using System.Text.RegularExpressions;
using System.Collections.Generic;

public class explodingMittensScript : MonoBehaviour {

    public KMBombModule Module;
    public KMAudio Audio;
    public KMBombInfo Info;
    public KMSelectable[] handSelectable;
    public KMSelectable drawSelectable, discardSelectable;
    public Material[] cardMats;
    public MeshRenderer[] handRenderer;
    public MeshRenderer discardRenderer;
    public GameObject[] handObjects, originalHandObjects;

    private static int _moduleIdCounter = 1;
    private int _moduleId;
    private bool solved = false;

    /*
        0-3: CIRCLE     0/4/8/12: RED
        4-7: HEART      1/5/9/13: BLUE
        8-11: TRIANGLE  2/6/10/14: GREEN
        12-15: SQUARE   3/7/11/15: YELLOW

        0:  NOPE
        1:  YEP
        2:  DEFUSE
        3:  DOUBLE PLAY
        4:  ATTACK
        5:  EXPLODING MITTEN

        0 1 2 3 4 5 6 7 8 9 10 11
        A B C D E F G H I J K  L
    */
    
    private int[] handCards = { 0, 0, 0 };
    private int tableNumber = 0;
    private readonly int[] tables =
    {
        0, 1, 1, 2, 4, 1, 3, 2, 0, 3, 4, 0, 4, 5, 2, 3,
        1, 4, 1, 3, 5, 0, 0, 2, 3, 4, 0, 4, 3, 2, 2, 1,
        0, 3, 5, 1, 4, 1, 2, 0, 4, 0, 2, 4, 2, 3, 1, 3,
        1, 4, 2, 3, 0, 2, 1, 0, 0, 2, 1, 5, 4, 3, 3, 4
    };
    private readonly int[] explodingMittenPositions = { 13, 4, 2, 11 };
    private readonly int[] ruleTable =
    {
        9, 4, 10, 0, 11, 7, 6, 9, 3, 0,
        6, 7, 10, 9, 8, 0, 10, 4, 5, 2,
        6, 1, 8, 2, 5, 8, 3, 1, 10, 11,
        3, 9, 1, 5, 7, 2, 11, 10, 11, 6,
        9, 1, 3, 11, 5, 8, 1, 4, 3, 8,
        2, 7, 5, 4, 7, 3, 5, 2, 6, 4
    };
    private int lastDigit = 0;
    private readonly string[] cardNames = { "red circle", "blue circle", "green circle", "yellow circle", "red heart", "blue heart", "green heart", "yellow heart", "red triangle", "blue triangle", "green triangle", "yellow triangle", "red square", "blue square", "green square", "yellow square" };
    private readonly string[] cardTypes = { "Nope", "Yep", "Defuse", "Double Play", "Attack", "Exploding Mitten" };
    private readonly string[] letters = { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L" };

    private int lastCard = 0;
    private int interval = 0;
    private int defuseIndex = 0;
    private int currentIndex = 0;

    private bool[] fucking = { false, false, false };
    private bool[] unfucking = { false, false, false };

    void Awake()
    {
        _moduleId = _moduleIdCounter++;
        drawSelectable.OnInteract += delegate () { if (!solved) { Draw(); } return false; };
        discardSelectable.OnInteract += delegate () { if (!solved) { Discard(); } return false; };
        for (int i = 0; i < 3; i++)
        {
            int j = i;
            handSelectable[i].OnInteract += delegate () { if (!solved) { Use(j); } return false; };
            handSelectable[i].OnHighlight += delegate () { StartCoroutine(Fuck(j)); };
            handSelectable[i].OnHighlightEnded += delegate () { StartCoroutine(Unfuck(j)); };
        }
        
    }

    private void Start()
    {
        if (Info.GetSerialNumberLetters().Any(x => x == 'A' || x == 'E' || x == 'I' || x == 'O' || x == 'U'))
            if (Info.GetSerialNumber().ToCharArray().Distinct().Count() != 6) { tableNumber = 0; } else { tableNumber = 1; }
        else
            if (Info.GetSerialNumber().ToCharArray().Distinct().Count() != 6) { tableNumber = 2; } else { tableNumber = 3; }
        lastDigit = Info.GetSerialNumberNumbers().Last();
        
        for (int i = 0; i < 3; i++)
        {
            handCards[i] = Random.Range(0, 16);
            while (tables[tableNumber * 16 + handCards[i]] == 5 || tables[tableNumber * 16 + handCards[i]] == 2)
                handCards[i] = Random.Range(0, 16);
        }

        lastCard = Random.Range(0, 16);
        while (tables[tableNumber * 16 + lastCard] == 5)
            lastCard = Random.Range(0, 16);
        discardRenderer.material = cardMats[lastCard];

        while (handCards.All(x => !CheckValidity(x, ruleTable[tables[tableNumber * 16 + lastCard] * 10 + lastDigit]) || tables[tableNumber * 16 + x] == 2 || tables[tableNumber * 16 + x] == 5))
            handCards[Random.Range(0, 2)] = Random.Range(0, 16);

        for (int i = 0; i < 3; i++)
            handRenderer[i].material = cardMats[handCards[i]];

        interval = Info.GetBatteryCount() % 5 + 9;
        defuseIndex = Random.Range(0, interval);
        currentIndex = Random.Range(1, interval);

        DebugMsg("You should use Table " + letters[tableNumber] + ".");
        DebugMsg("The card on the discard pile is a " + cardNames[lastCard] + ", which is a(n) " + cardTypes[tables[tableNumber * 16 + lastCard]] + " card.");
        DebugMsg("The rule used is " + letters[ruleTable[tables[tableNumber * 16 + lastCard] * 10 + lastDigit]] + ".");
        for (int i = 0; i < 3; i++)
            DebugMsg("Card #" + (i+1) + " is a " + cardNames[handCards[i]] + ", which is a(n) " + cardTypes[tables[tableNumber * 16 + handCards[i]]] + " card.");
    }

    void Draw()
    {
        int placeholder = Random.Range(0, 16);
        if (currentIndex == interval)
        {
            DebugMsg("You were going to draw an Exploding Mitten. Strike!");
            Module.HandleStrike();
        }
        else if (!handCards.Contains(99))
        {
            DebugMsg("Your hand was full. Strike!");
            Module.HandleStrike();
        }
        else
        {
            for (int i = 0; i < 3; i++)
                if (handCards[i] == 99)
                {
                    if (currentIndex == defuseIndex)
                        while (tables[tableNumber * 16 + placeholder] != 2)
                            placeholder = Random.Range(0, 16);
                    else if (handCards[1] != 99 && handCards.All(x => !CheckValidity(x, ruleTable[tables[tableNumber * 16 + lastCard] * 10 + lastDigit])))
                        while (tables[tableNumber * 16 + placeholder] == 2 || tables[tableNumber * 16 + placeholder] == 5 || !CheckValidity(placeholder, ruleTable[tables[tableNumber * 16 + lastCard] * 10 + lastDigit]))
                            placeholder = Random.Range(0, 16);
                    else
                        while (tables[tableNumber * 16 + placeholder] == 2 || tables[tableNumber * 16 + placeholder] == 5)
                            placeholder = Random.Range(0, 16);
                    handCards[i] = placeholder;
                    break;
                }

            UpdateModule();
            currentIndex++;

            DebugMsg("You drew a " + cardNames[placeholder] + ", which is a(n) " + cardTypes[tables[tableNumber * 16 + placeholder]] + " card.");
            DebugMsg("(The Exploding Mitten is coming in " + (interval - currentIndex + 1) + " draws.)");

            Audio.PlaySoundAtTransform("card" + Random.Range(1, 10).ToString(), Module.transform);
        }
    }

    void Discard()
    {
        int placeholder = Random.Range(0, 16);
        bool[] playableCards = { false, false, false };

        for (int i = 0; i < 3; i++)
            if (handCards[i] != 99)
            {
                playableCards[i] = CheckValidity(handCards[i], ruleTable[tables[tableNumber * 16 + lastCard] * 10 + lastDigit]);
            }

        if (playableCards.Count(x => x) == 1)
            for (int i = 0; i < 3; i++)
                if (playableCards[i] && tables[tableNumber * 16 + handCards[i]] == 2)
                    playableCards[i] = false;
        
        if (playableCards.Any(x => x))
        {
            DebugMsg("You tried to click on the discard pile, but you still could've played:");
            if (playableCards[0])
                DebugMsg("... the first card! Woah!");
            if (playableCards[1])
                DebugMsg("... the second card! Wow!");
            if (playableCards[2])
                DebugMsg("... the third card! Wacky!");
            DebugMsg("Strike!");
            Module.HandleStrike();
        }
        else
        {
            if (currentIndex == interval)
                while (tables[tableNumber * 16 + placeholder] != 5)
                    placeholder = Random.Range(0, 16);
            else if (currentIndex == defuseIndex)
                while (tables[tableNumber * 16 + placeholder] != 2)
                    placeholder = Random.Range(0, 16);
            else
                while (tables[tableNumber * 16 + placeholder] == 2 || tables[tableNumber * 16 + placeholder] == 5)
                    placeholder = Random.Range(0, 16);
            lastCard = placeholder;

            UpdateModule();
            currentIndex++;

            DebugMsg("You discarded a " + cardNames[placeholder] + ", which is a(n) " + cardTypes[tables[tableNumber * 16 + placeholder]] + " card.");
            DebugMsg("The rule used now is " + letters[ruleTable[tables[tableNumber * 16 + lastCard] * 10 + lastDigit]] + ".");
            DebugMsg("(The Exploding Mitten is coming in " + (interval - currentIndex + 1) + " draws.)");

            Audio.PlaySoundAtTransform("card" + Random.Range(1, 10).ToString(), Module.transform);
        }
    }

    void Use(int cardToDiscard)
    {
        if (tables[tableNumber * 16 + handCards[cardToDiscard]] == 2 && tables[tableNumber * 16 + lastCard] == 5)
        {
            DebugMsg("You played a Defuse card on an Exploding Mitten card. Module solved!");
            Module.HandlePass();
            solved = true;

            lastCard = handCards[cardToDiscard];
            for (int i = 0; i < 3; i++)
                handCards[i] = 99;

            Audio.PlaySoundAtTransform("card" + Random.Range(1, 10).ToString(), Module.transform);
            UpdateModule();
        }

        else if (CheckValidity(handCards[cardToDiscard], ruleTable[tables[tableNumber * 16 + lastCard] * 10 + lastDigit]))
        {
            DebugMsg("You played a " + cardNames[handCards[cardToDiscard]] + ", which is a(n) " + cardTypes[tables[tableNumber * 16 + handCards[cardToDiscard]]] + " card.");
            lastCard = handCards[cardToDiscard];

            handCards[cardToDiscard] = 99;

            if (handCards[0] == 99) { handCards[0] = handCards[1]; handCards[1] = 99; }
            if (handCards[1] == 99) { handCards[1] = handCards[2]; handCards[2] = 99; }

            Audio.PlaySoundAtTransform("card" + Random.Range(1, 10).ToString(), Module.transform);
            UpdateModule();
            DebugMsg("The rule used is " + letters[ruleTable[tables[tableNumber * 16 + lastCard] * 10 + lastDigit]] + ".");
        }

        else
        {
            DebugMsg("The " + cardNames[handCards[cardToDiscard]] + " card was invalid. Strike!");
            Module.HandleStrike();
        }
    }

    bool CheckValidity(int card, int rule)
    {
        if (card == 99)
            return false;

        bool ghfhgfjfghghgje;
        switch (rule)
        {
            case 0:
                ghfhgfjfghghgje = card == lastCard;
                break;
            case 1:
                ghfhgfjfghghgje = card % 4 == lastCard % 4;
                break;
            case 2:
                ghfhgfjfghghgje = Mathf.Floor(card / 4) == Mathf.Floor(lastCard / 4);
                break;
            case 3:
                ghfhgfjfghghgje = card % 4 != lastCard % 4 && Mathf.Floor(card / 4) != Mathf.Floor(lastCard / 4);
                break;
            case 4:
                ghfhgfjfghghgje = card % 4 != lastCard % 4;
                break;
            case 5:
                ghfhgfjfghghgje = Mathf.Floor(card / 4) != Mathf.Floor(lastCard / 4);
                break;
            case 6:
                ghfhgfjfghghgje = card % 4 == 3;
                break;
            case 7:
                ghfhgfjfghghgje = card % 4 != 0;
                break;
            case 8:
                ghfhgfjfghghgje = Mathf.Floor(card / 4) == Mathf.Floor(explodingMittenPositions[tableNumber] / 4);
                break;
            case 9:
                ghfhgfjfghghgje = Mathf.Floor(card / 4) != Mathf.Floor(explodingMittenPositions[tableNumber] / 4);
                break;
            case 10:
                ghfhgfjfghghgje = card % 4 == explodingMittenPositions[tableNumber] % 4;
                break;
            case 11:
                ghfhgfjfghghgje = card % 4 != explodingMittenPositions[tableNumber] % 4;
                break;
            default:
                ghfhgfjfghghgje = true;
                break;
        }
        return ghfhgfjfghghgje;
    }

    void UpdateModule()
    {
        discardRenderer.material = cardMats[lastCard];
        for (int i = 0; i < 3; i++)
        {
            if (handCards[i] == 99)
                handObjects[i].SetActive(false);
            else
            {
                handObjects[i].SetActive(true);
                handRenderer[i].material = cardMats[handCards[i]];
            }
        }

        if (currentIndex > interval)
        {
            defuseIndex = Random.Range(0, interval);
            currentIndex = 1;
        }
    }
    
    IEnumerator Fuck(int cardToMove)
    {
        while (fucking[cardToMove])
            yield return new WaitForSeconds(.01f);
        unfucking[cardToMove] = false;
        fucking[cardToMove] = true;
        var ughghhhghg = new Vector3(0, 0, .005f);
        float variableName = .01f;
        for (int i = 0; i < 10; i++)
        {
            handObjects[cardToMove].transform.localPosition += ughghhhghg * variableName;
            variableName += .1f;
            yield return new WaitForSeconds(.01f);
        }
        fucking[cardToMove] = false;
    }

    IEnumerator Unfuck(int cardToMove)
    {
        while (unfucking[cardToMove])
            yield return new WaitForSeconds(.01f);
        fucking[cardToMove] = false;
        unfucking[cardToMove] = true;
        var ughghhhghg = new Vector3(0, 0, -.005f);
        float variableName = .01f;
        for (int i = 0; i < 10; i++)
        {
            if (handObjects[cardToMove].transform.localPosition.z < originalHandObjects[cardToMove].transform.localPosition.z)
            {
                handObjects[cardToMove].transform.localPosition = originalHandObjects[cardToMove].transform.localPosition;
                break;
            }
            handObjects[cardToMove].transform.localPosition += ughghhhghg * variableName;
            variableName += .1f;
            yield return new WaitForSeconds(.01f);
        }
        unfucking[cardToMove] = false;
    }

    void DebugMsg(string message)
    {
        Debug.LogFormat("[Exploding Mittens #{0}] {1}", _moduleId, message);
    }

    //twitch plays
    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} play <#> [Plays the specified card from left to right (1-3)] | !{0} draw [Draws the top card of the deck] | !{0} discard [Discards the top card of the deck]";
    #pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        if (Regex.IsMatch(command, @"^\s*draw\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            drawSelectable.OnInteract();
        }
        if (Regex.IsMatch(command, @"^\s*discard\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            discardSelectable.OnInteract();
        }
        string[] parameters = command.Split(' ');
        if (Regex.IsMatch(parameters[0], @"^\s*play\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            if (parameters.Length > 2)
            {
                yield return "sendtochaterror Too many parameters!";
            }
            else if (parameters.Length == 2)
            {
                if (!parameters[1].EqualsAny("1", "2", "3"))
                {
                    yield return "sendtochaterror!f The specified card to play '" + parameters[1] +  "' is invalid!";
                    yield break;
                }
                int index = int.Parse(parameters[1]) - 1;
                if (!handObjects[index].activeSelf)
                {
                    yield return "sendtochaterror There is not currently a card in that position!";
                    yield break;
                }
                handSelectable[index].OnInteract();
            }
            else if (parameters.Length == 1)
            {
                yield return "sendtochaterror Please specify which card you wish to play!";
            }
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        while (true)
        {
            List<int> valids = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                if (handObjects[i].activeSelf)
                {
                    if (CheckValidity(handCards[i], ruleTable[tables[tableNumber * 16 + lastCard] * 10 + lastDigit]) && tables[tableNumber * 16 + handCards[i]] != 2)
                        valids.Add(i);
                }
            }
            if (valids.Count == 0)
            {
                if (currentIndex == defuseIndex)
                {
                    drawSelectable.OnInteract();
                    yield return new WaitForSeconds(0.1f);
                }
                else
                {
                    discardSelectable.OnInteract();
                    yield return new WaitForSeconds(0.1f);
                }
                if (currentIndex == interval + 1)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        if (handObjects[i].activeSelf)
                        {
                            if (tables[tableNumber * 16 + handCards[i]] == 2)
                            {
                                handSelectable[i].OnInteract();
                                yield break;
                            }
                        }
                    }
                }
            }
            else
            {
                handSelectable[valids.PickRandom()].OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
        }
    }
}
