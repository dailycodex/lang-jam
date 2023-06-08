using Godot;
using System;
using System.Collections;
using System.Collections.Generic;

public enum COMMANDS
{
    NOCOMMAND,
    FIND,
    REPLACE,
    APPEND,
    ONCE,
    STOPIFCONTAINS,
    STORAGE,
    FINDFROMSTRG,
    DELETE,
    FINDR,
    SEEK,
    FAILIFFOUND,
    FAILIFFOUNDR,
}

public enum SEEKTARGETS
{
    END,
    BEGINNING,
    STARTOFSELECTION,
    STARTOFLINE,
}


public class LangRoot : Control
{
    public TextEdit TextEdit;
    public TextEdit TextDisplay;
    public TextEdit AppendText;
    public Button ParseButton;
    public Label ValidityLabel;
    public Button AssembleButton;
    public Label ProblemLabel;
    public Label CurrentCommandLabel;
    public Label CurrentPartLabel;
    public Label ExecutionFromLabel;
    public Button StepButton;
    public Label CurrentPartNumberLabel;
    public Label ExecutionStoppedLabel;
    public Button OpenMenuButton;
    public Panel MenuPanel;
    public TextEdit TextLookup;
    public Label OneSuccessLabel;

    private int _executingFromChar;
    public int ExecutingFromChar
    {
        get {return _executingFromChar;}
        set {_executingFromChar = value; ExecutionFromLabel.Text = value.ToString();}
    }

    private string _currentCommand;
    public string CurrentCommand
    {
        get {return _currentCommand;}
        set {_currentCommand = value; CurrentCommandLabel.Text = value; CurrentCommandLabel.GetParent<ScrollContainer>().ScrollHorizontal = 0;}
    }

    private string _assembledField;
    public string AssembledField
    {
        get {return _assembledField;}
        set {
            _assembledField = value;
            double scroll = TextDisplay.ScrollVertical; 
            int scrollH = TextDisplay.ScrollHorizontal; 
            TextDisplay.Text = value; 
            TextDisplay.ScrollVertical = scroll;
            TextDisplay.ScrollHorizontal = scrollH;
        }
    }

    private bool _problemEncountered;
    public bool ProblemEncountered
    {
        get {return _problemEncountered;}
        set {_problemEncountered = value; string labelString = value ? "Problem encountered!" : "No Problems!"; ProblemLabel.Text = labelString;}
    }

    private int _currentCommandPartNumber;
    public int CurrentCommandPartNumber
    {
        get {return _currentCommandPartNumber;}
        set {_currentCommandPartNumber = value; CurrentPartNumberLabel.Text = value.ToString();}
    }

    private (int start, int len) _selected;
    public (int Start, int Length) Selected
    {
        get {return _selected;}
        set {
            _selected = value;
            //GD.Print(value);
            (int, int, int, int) t = CharsNToSelection(value.Item1, value.Item2, AssembledField);
            TextDisplay.Deselect();
            if (value.Length == 0)
            {
                return;
            }
            else
            {
                TextDisplay.CallDeferred("select", t.Item1, t.Item2, t.Item3, t.Item4);
            }
        }
    }


    private bool _executionFinished;
    public bool ExecutionFinished
    {
        get {return _executionFinished;}
        set {_executionFinished = value; ExecutionStoppedLabel.Visible = value;}
    }


    private bool _oneSuccessThisPass;
    public bool OneSuccessThisPass
    {
        get {return _oneSuccessThisPass;}
        set {_oneSuccessThisPass = value; OneSuccessLabel.Text = value ? "Yes" : "No";}
    }


    public override void _Ready()
    {
        TextEdit = GetNode<TextEdit>("%TextEdit");
        TextDisplay = GetNode<TextEdit>("%TextDisplay");
        ParseButton = GetNode<Button>("%ParseButton");
        AppendText = GetNode<TextEdit>("%AppendText");
        ValidityLabel = GetNode<Label>("%ValidityLabel");
        AssembleButton = GetNode<Button>("%AssembleButton");
        ProblemLabel = GetNode<Label>("%ProblemLabel");
        CurrentCommandLabel = GetNode<Label>("%CurrentCommandLabel");
        CurrentPartLabel = GetNode<Label>("%CurrentPartLabel");
        ExecutionFromLabel = GetNode<Label>("%ExecutionFromLabel");
        StepButton = GetNode<Button>("%StepButton");
        CurrentPartNumberLabel = GetNode<Label>("%CurrentPartNumberLabel");
        ExecutionStoppedLabel = GetNode<Label>("%ExecutionStoppedLabel");
        OpenMenuButton = GetNode<Button>("%OpenMenuButton");
        MenuPanel = GetNode<Panel>("%MenuPanel");
        TextLookup = GetNode<TextEdit>("%TextLookup");
        OneSuccessLabel = GetNode<Label>("%OneSuccessLabel");

        //ParseButton.Connect("pressed", this, nameof(OnParsePressed));
        AssembleButton.Connect("pressed", this, nameof(OnAssembleButtonPressed));
        StepButton.Connect("pressed", this, nameof(OnStepButtonPressed));   
        OpenMenuButton.Connect("pressed", this, nameof(OnOpenMenuButtonPressed));

        ExecutingFromChar = 0;
        CurrentCommand = "";
        AssembledField = "";
        Selected = default((int, int));
        ExecutionFinished = false;
        MenuPanel.Visible = false;
        OneSuccessThisPass = false;
        
    }


    public override void _UnhandledInput(InputEvent @event)
    {
        base._UnhandledInput(@event);

        if (@event is InputEventKey)
        {
            InputEventKey e = @event as InputEventKey;
            if (e.Pressed && e.Scancode == (uint) KeyList.Key1)
            {
                OnStepButtonPressed();
            }
        }
    }


    public void OnStepButtonPressed()
    {
        if (AssembledField == "")
        {
            return;
        }
        if (ExecutionFinished)
        {
            return;
        }

        if (CurrentCommand == "")
        {   
            SeekForCommandOuter();
            return;
        }

        string [] splitCommand = SplitEscaped(CurrentCommand);

        if (splitCommand.Length < CurrentCommandPartNumber + 1)   //if reached last part of command
        {
            SeekForCommandOuter();
            return;
        }

        COMMANDS command;
        List<string> arguments = new List<string>();

        try
        {   //extracting part type and arguments
            string unparsedCommand = splitCommand[CurrentCommandPartNumber];
            command = ParsePart(unparsedCommand);
            if (command == COMMANDS.NOCOMMAND)
            {
                throw new FailedToParseException("Not recognized a command!");
            }
            else if (command == COMMANDS.ONCE)
            {
                //do nothing to arguments
            }
            else if (command != COMMANDS.STORAGE)
            {
                string argumentCandidate = splitCommand[CurrentCommandPartNumber + 1];
                if (ParsePart(argumentCandidate) != COMMANDS.NOCOMMAND) //player didnt put in a command right after current without argument for current
                {
                    throw new FailedToParseException("No argument for command!");
                }
                else
                {
                    arguments.Add(argumentCandidate);
                }
            }
            else
            {   //for STORAGE
                string storageName = splitCommand[CurrentCommandPartNumber + 1];
                if (storageName.Length > 1 || !Char.IsLetter(storageName[0]))
                {
                    throw new FailedToExecuteCommandException("Storage name not specified correctly!");
                }
                for (int i = CurrentCommandPartNumber + 1; i < splitCommand.Length; i++)
                {
                    string argument = splitCommand[i];
                    if (ParsePart(argument) != COMMANDS.NOCOMMAND)
                    {
                        throw new FailedToExecuteCommandException("Can't put more commands after STORAGE!");
                    }
                    arguments.Add(argument);
                }
            }
        }
        catch (Exception e)
        {
            //GD.Print(e.Message);
            CurrentCommand = "";
            Selected = default((int, int));
            return;
        }

        try
        {   //execution of a command part
            int from = Math.Max(ExecutingFromChar, FindDivider(AssembledField));
            from = Math.Max(from, Selected.Start + Selected.Length);
            ExecuteCommand(command, from, arguments.ToArray());
            //executed successfully because there is no exception
            OneSuccessThisPass = true;
            CurrentCommandPartNumber += 1 + arguments.Count;
            return;
        }
        catch (FailedToExecuteCommandException e)
        {
            //GD.Print(e.Message);
            CurrentCommand = "";
            Selected = default((int, int));
            return;
        }
    }


    public string[] SplitEscaped(string command)
    {
        List<String> lst = new List<string>();

        int currentIndex = -1;
        int previousDivider = -1;
        do
        {
            int nextDivider = command.Find('|', currentIndex + 1);

            if (nextDivider < 0)
            {
                previousDivider = previousDivider < 0 ? 0 : previousDivider;
                lst.Add(command.Substr(previousDivider + 1, command.Length - previousDivider - 1));

                currentIndex = command.Length;
                continue;
            }            
            else if (nextDivider == 0)
            {
                lst.Add("");
                previousDivider = 0;
            }
            else
            {
                char prevCh = command[nextDivider - 1];
                if (prevCh == '&')
                {
                    //idk
                }
                else
                {
                    lst.Add(command.Substr(previousDivider + 1, nextDivider - previousDivider - 1));
                    previousDivider = nextDivider;
                }
            }
            currentIndex = nextDivider + 1;
        } while (currentIndex < command.Length);

        return lst.ToArray();
    }


    public void SeekForCommandOuter()
    {
        if (ExecutingFromChar == 0)
        {
            OneSuccessThisPass = false;
        }

        (int, int, string) foundCommand = SeekForCommand(ExecutingFromChar, AssembledField, false, true);

        if (foundCommand.Item1 < 0 || foundCommand.Item2 < 0)
        {
            if (!OneSuccessThisPass)
            {
                ExecutionFinished = true;
                return;
            }
            ExecutingFromChar = 0;
            return;
        }

        CurrentCommand = foundCommand.Item3;
        ExecutingFromChar = foundCommand.Item2;
        CurrentCommandPartNumber = 0;
        return;
    }


    public void DeleteCommand()
    {
        (int, int, string) foundCommand = SeekForCommand(ExecutingFromChar + 1, AssembledField, true, false);
        if (foundCommand.Item1 < 0 || foundCommand.Item2 < 0)
        {
            throw new FailedToParseException("Somethings wrong!");
        }
        AssembledField = AssembledField.Substr(0, foundCommand.Item1 - 1) + AssembledField.Substr(foundCommand.Item2 + 1, AssembledField.Length - foundCommand.Item2 - 1);
        Selected = default((int, int));
    }


    public static int FindDivider(string field)
    {
        return field.FindLast("~") + 1;
    }


    public void FinishExecution()
    {
        CurrentCommand = "";
        Selected = default((int, int));
        ExecutionFinished = true;
    }


    public void ExecuteCommand(COMMANDS command, int from, params string[] arguments)
    {
        //GD.Print(command, from, arguments);
        CurrentPartLabel.Text = command.ToString() + "|" + String.Join("|", arguments);

        string argument = command == COMMANDS.ONCE ? null : arguments[0];
        if (command != COMMANDS.FINDR && command != COMMANDS.FAILIFFOUNDR && command != COMMANDS.ONCE)
        {
            argument = System.Text.RegularExpressions.Regex.Unescape(argument);
        }
        
        switch (command)
        {
            case COMMANDS.FIND:
                ExecuteFIND(from, argument);
                break;
            case COMMANDS.FINDR:
                ExecuteFINDR(from, argument, false);
                break;
            case COMMANDS.REPLACE:
                ExecuteREPLACE(from, argument);
                break;
            case COMMANDS.APPEND:
                ExecuteAPPEND(argument);
                break;
            case COMMANDS.ONCE:
                ExecuteONCE();
                break;
            case COMMANDS.DELETE:
                ExecuteDELETE(argument);
                break;
            case COMMANDS.STOPIFCONTAINS:
                ExecuteSTOPIFCONTAINS(argument);
                break;
            case COMMANDS.STORAGE:
                ExecuteSTORAGE();
                break;
            case COMMANDS.FINDFROMSTRG:
                ExecuteFINDFROMSTORAGE(from, argument);
                break;
            case COMMANDS.SEEK:
                ExecuteSEEK(from, argument);
                break;
            case COMMANDS.FAILIFFOUND:
                ExecuteFAILIFFOUND(from, argument);
                break;
            case COMMANDS.FAILIFFOUNDR:
                ExecuteFAILIFFOUNDR(from, argument, false);
                break;
            default:
                throw new Exception("Not implemented");
        }

        //throw new FailedToExecuteCommandException("test");  //shouldnt be called always
    }


    public void ExecuteSTORAGE()
    {
        throw new FailedToExecuteCommandException("Storage");
    }


    public void ExecuteFIND(int from, string argument)
    {
        ExecuteFINDR(from, argument, true);   
    }


    public void ExecuteFINDR(int from, string argument, bool dumb = false)
    {
        System.Text.RegularExpressions.Regex patten = new System.Text.RegularExpressions.Regex(argument);

        if (from >= AssembledField.Length)
        {
            from = AssembledField.Length - 1;
        }

        int length;
        int foundPos = -1;
        if (dumb)
        {
            foundPos = AssembledField.Find(argument, from);
            length = argument.Length;
        }
        else
        {
            System.Text.RegularExpressions.Match match = patten.Match(AssembledField, from);
            if (match.Success)
            {
                foundPos = match.Index;
                length = match.Length;
            }
            else
            {
                length = -1;
                foundPos = -1;
            }
        }
        if (foundPos < 0)
        {
            throw new FailedToExecuteCommandException("FIND " + argument);
        }
        Selected = (foundPos, length);
    }


    public void ExecuteFAILIFFOUND(int from, string argument)
    {
        ExecuteFAILIFFOUNDR(from, argument, true);
    }


    public void ExecuteFAILIFFOUNDR(int from, string argument, bool dumb = false)
    {
        System.Text.RegularExpressions.Regex patten = new System.Text.RegularExpressions.Regex(argument);

        if (from >= AssembledField.Length)
        {
            from = AssembledField.Length - 1;
        }

        int length;
        int foundPos = -1;
        if (dumb)
        {
            foundPos = AssembledField.Find(argument, from);
            length = argument.Length;
        }
        else
        {
            System.Text.RegularExpressions.Match match = patten.Match(AssembledField, from);
            if (match.Success)
            {
                foundPos = match.Index;
                length = match.Length;
            }
            else
            {
                length = -1;
                foundPos = -1;
            }
        }
        if (foundPos >= 0)
        {
            Selected = (foundPos, argument.Length);
            throw new FailedToExecuteCommandException("Failed to find");
        }
        
    }


    public void ExecuteFINDFROMSTORAGE(int from, string argument)
    {
        System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex("STORAGE\\|" + argument);
        System.Text.RegularExpressions.Match match = pattern.Match(AssembledField);
        if (!match.Success)
        {
            throw new FailedToExecuteCommandException("Not found a storage with this name!");
        }

        int nextBrace = AssembledField.Find("}", match.Index);
        string storage = AssembledField.Substr(match.Index + match.Length + 1, nextBrace - match.Index - match.Length - 1);
        string[] storageSplit = storage.Split("|");

        int closestMatch = -1;
        string closestFound = "";

        if (from >= AssembledField.Length)
        {
            from = AssembledField.Length - 1;
        }

        foreach (string storedArgument in storageSplit)
        {
            int foundIdx = AssembledField.Find(storedArgument, from);
            if (closestMatch < 0 || (foundIdx >= 0 && foundIdx < closestMatch))
            {
                closestMatch = foundIdx;
                closestFound = storedArgument;
            }
        }
        if (closestMatch < 0)
        {
            throw new FailedToExecuteCommandException("Nothing found!");
        }
        Selected = (closestMatch, closestFound.Length);
    }


    public void ExecuteREPLACE(int from, string argument)
    {
        argument = EscapeCharacters(argument);
        string firstPart = AssembledField.Substr(0, Selected.Start);
        int lastPartLength = AssembledField.Length - (Selected.Start + Selected.Length);
        lastPartLength = Math.Min(lastPartLength, AssembledField.Length - (Selected.Start + Selected.Length));
        lastPartLength = Math.Max(lastPartLength, 0);
        int startOfLastPart = Selected.Start + Selected.Length;
        startOfLastPart = Math.Min(startOfLastPart, AssembledField.Length - 1);
        string lastPart = AssembledField.Substr(startOfLastPart, lastPartLength);
        AssembledField = firstPart + argument + lastPart;
        Selected = (Selected.Start, argument.Length);
    }


    public void ExecuteAPPEND(string argument)
    {
        argument = EscapeCharacters(argument);
        int endOfSelected = Selected.Start + Selected.Length;
        AssembledField = AssembledField.Insert(endOfSelected, argument);
        Selected = (Selected.Start, Selected.Length + argument.Length);
    }


    public static string EscapeCharacters(string argument)
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        char previousCh = default(char);
        foreach (char ch in argument)
        {
            if (ch == '&')
            {
                if (previousCh == '&')
                {
                    builder.Append(ch);
                }
                else
                {
                    //do nothing
                }
            }
            else
            {
                builder.Append(ch);
            }
            previousCh = ch;
        }
        return builder.ToString();
    }


    public void ExecuteONCE()
    {
        DeleteCommand();
    }


    public void ExecuteDELETE(string argument)
    {
        string selected = AssembledField.Substr(Selected.Start, Selected.Length);
        int foundIdx = selected.Find(argument);
        if (foundIdx < 0)
        {
            throw new FailedToExecuteCommandException("Delete argument not found!");
        }
        foundIdx += Selected.Start;
        string firstPart = AssembledField.Substr(0, foundIdx);
        string lastPart = AssembledField.Substr(foundIdx + argument.Length, AssembledField.Length - (foundIdx + argument.Length));
        AssembledField = firstPart + lastPart;
        Selected = (foundIdx, 0);
    }


    public void ExecuteSTOPIFCONTAINS(string argument)
    {
        string selected = AssembledField.Substr(Selected.Start, Selected.Length);
        int found = selected.Find(argument);
        if (found < 0)
        {
            //idk
        }
        else
        {
            FinishExecution();
        }
    }


    public void ExecuteSEEK(int from, string argument)
    {
        var selectedBackup = Selected;
        SEEKTARGETS a;
        try
        {
            a= (SEEKTARGETS) Enum.Parse(typeof(SEEKTARGETS), argument);
        }
        catch
        {
            throw new FailedToExecuteCommandException("Wrong SEEK argument!");
        }
        
        switch (a)
        {
            case SEEKTARGETS.END:
                Selected = (AssembledField.Length - 1, 0);
                break;
            case SEEKTARGETS.BEGINNING:
                Selected = (FindDivider(AssembledField), 0);
                break;
            case SEEKTARGETS.STARTOFSELECTION:
                Selected = (Selected.Start, 0);
                break;
            case SEEKTARGETS.STARTOFLINE:
                int prevNewline = AssembledField.Substr(0, from).FindLast("\n");
                if (prevNewline < 0)
                {
                    Selected = (FindDivider(AssembledField), 0);
                }
                else
                {
                    Selected = (prevNewline + 1, 0);
                }
                break;
            default:
                throw new FailedToExecuteCommandException("Wrong SEEK argument!");
        }

        if (Selected == selectedBackup)
        {
            throw new FailedToExecuteCommandException("Already seeked here!");
        }
    }


    public static (int fromLine, int fromColumn, int toLine, int toColumn) CharsNToSelection(int charsFromStart, int lengthOfSelection, string field)
    {
        lengthOfSelection = Math.Min(lengthOfSelection, field.Length - charsFromStart);

        int newlinesFromStart = charsFromStart == 0 ? 0 : field.Count("\n", false, 0, charsFromStart);
        int lengthOfPreviousLine = charsFromStart - 1 - field.Substr(0, charsFromStart).FindLast("\n");

        int fieldLength = field.Length; //temp
        int newlinesTillEnd = field.Count("\n", false, charsFromStart, charsFromStart + lengthOfSelection) + newlinesFromStart;
        int lengthOfLastLine = charsFromStart + lengthOfSelection - 1 - field.Substr(0, charsFromStart + lengthOfSelection).FindLast("\n");

        return (newlinesFromStart, lengthOfPreviousLine, newlinesTillEnd, lengthOfLastLine);
    }

    //returns (start, end, command text)
    public (int start, int end, string) SeekForCommand(int from, string field, bool backwards = false, bool ignoreSTORAGE = true)
    {
        if (backwards)
        {
            int foundEnd = SeekForUnescapedBackwards(field, from, '}');

            if (foundEnd < 0)
            {
                return (-1, -1, "");
            }

            int foundStart = SeekForUnescapedBackwards(field, foundEnd, '{');

            if (foundStart < 0)
            {
                return (-1, -1, "");
            }

            return (foundStart + 1, foundEnd, field.Substr(foundStart + 1, foundEnd - foundStart - 1));
        }
        else
        {
            int foundStart = SeekForUnescaped(field, from, '{');

            if (foundStart < 0)
            {
                return (-1, -1, "");
            }

            int foundEnd = SeekForUnescaped(field, foundStart, '}');

            if (foundEnd < 0)
            {
                return (-1, -1, "");
            }

            return (foundStart + 1, foundEnd, field.Substr(foundStart + 1, foundEnd - foundStart - 1));
        }
    }


    public int SeekForUnescapedBackwards(string field, int from, char toSeek)
    {
        int foundCharIdx = -1;
        do
        {
            int foundSoFar = field.Substr(0, from).FindLast(new String(new char[] {toSeek}));
            if (foundSoFar < 0)
            {
                from = -1;
            }
            else if (foundSoFar == 0)
            {
                foundCharIdx = foundSoFar;
            }
            else
            {
                char prev = field[foundSoFar - 1];
                if (prev == '&')
                {
                    from = foundSoFar - 1;
                    continue;
                }
                else
                {
                    foundCharIdx = foundSoFar;
                }
            }
        } while (from > 0 && foundCharIdx < 0);
        return foundCharIdx;
    }


    public int SeekForUnescaped(string field, int from, char toSeek)
    {
        int foundCharIdx = -1;
        do
        {
            int foundSoFar = field.Find(toSeek, from);
            if (foundSoFar < 0)
            {
                from = field.Length;
            }
            else if (foundSoFar == 0)
            {
                foundCharIdx = foundSoFar;
            }
            else
            {
                char prev = field[foundSoFar - 1];
                if (prev == '&')
                {
                    from = foundSoFar + 1;
                    continue;
                }
                else
                {
                    foundCharIdx = foundSoFar;
                }
            }
        } while (foundCharIdx < 0 && from < field.Length);

        return foundCharIdx;
    }
/*
    public (int, int, string) SeekForCommand(int from, string seekIn, bool backwards = false, bool ignoreSTORAGE = true)
    {
        int startingCurlyBrace = backwards ? seekIn.Substr(0, from).FindLast("}") : seekIn.Find("{", from);
        if (startingCurlyBrace < 0)
        {
            return (-1, -1, "");
        }

        int endingCurlyBrace = backwards ? seekIn.Substr(0, startingCurlyBrace).FindLast("{") : seekIn.Find("}", startingCurlyBrace);
        if (endingCurlyBrace < 0)
        {
            return (-1, -1, "");
        }
        int sTORAGEiDX = backwards ? seekIn.Substr(0, endingCurlyBrace).FindLast("STORAGE") : seekIn.Find("STORAGE", startingCurlyBrace);
        if (ignoreSTORAGE && sTORAGEiDX > 0)
        {
            if (backwards)
            {       //idk if this part works
                if (sTORAGEiDX > endingCurlyBrace)  //if this command is STORAGE
                {                                   //seek further
                    return SeekForCommand(startingCurlyBrace, seekIn, true, true);
                }
            }
            else
            {       //but this one does
                if (sTORAGEiDX < endingCurlyBrace)  //if this command is STORAGE
                {                                   //seek further
                    return SeekForCommand(endingCurlyBrace, seekIn, false, true);
                }
            }
        }

        int start = backwards ? endingCurlyBrace : startingCurlyBrace;
        int end = backwards ? startingCurlyBrace : endingCurlyBrace;

        return (start + 1, end, seekIn.Substr(start + 1, end - start - 1));
    }
*/

    public void OnAssembleButtonPressed()
    {
        string commands = TextEdit.Text;
        commands = commands.StripEdges();

        string appendText = AppendText.Text;
        appendText = appendText.StripEdges();
        appendText += "\n";

        string outputText = commands + "\n~~~\n" + appendText;
        outputText = ToUpperCustom(outputText);
        AssembledField = outputText;

        ExecutingFromChar = 0;
        ExecutionFinished = false;

        //Selected = (7,3);//temp
        //CallDeferred("temp", 0,0);
    }


    public void temp(int a, int b)
    {
        Selected = (a,b);
    }


    public static string ToUpperCustom(string input)
    {
        System.Text.StringBuilder b = new System.Text.StringBuilder();

        char prevCh = default(char);
        foreach (char ch in input)
        {
            if (prevCh == '\\')
            {
                b.Append(ch);
            }
            else
            {
                b.Append(Char.ToUpper(ch));
            }
            prevCh = ch;
        }

        return b.ToString();
    }


    public void OnOpenMenuButtonPressed()
    {
        MenuPanel.Visible = true;
    }


    public void _on_CloseButton_pressed()
    {
        MenuPanel.Visible = false;
    }


    public void _on_Button_pressed()
    {
        TextLookup.Text = MenuPanel.GetNode("Button").EditorDescription;
    }


    public void _on_Button2_pressed()
    {
        TextLookup.Text = MenuPanel.GetNode("Button2").EditorDescription;
    }


    public void _on_Button3_pressed()
    {
        TextLookup.Text = MenuPanel.GetNode("Button3").EditorDescription;
    }


    public void _on_Button4_pressed()
    {
        TextLookup.Text = MenuPanel.GetNode("Button4").EditorDescription;
    }


    public void _on_Button5_pressed()
    {
        TextLookup.Text = MenuPanel.GetNode("Button5").EditorDescription;
    }


    public void _on_Button6_pressed()
    {
        TextLookup.Text = MenuPanel.GetNode("Button6").EditorDescription;
    }

    
    public void _on_Button7_pressed()
    {
        TextLookup.Text = MenuPanel.GetNode("Button7").EditorDescription;
    }


    public void _on_Button8_pressed()
    {
        TextLookup.Text = MenuPanel.GetNode("Button8").EditorDescription;
    }


    public void _on_Button9_pressed()
    {
        TextLookup.Text = MenuPanel.GetNode("Button9").EditorDescription;
    }


    public void _on_Button10_pressed()
    {
        TextLookup.Text = MenuPanel.GetNode("Button10").EditorDescription;
    }


    public void _on_Button11_pressed()
    {
        TextLookup.Text = MenuPanel.GetNode("Button11").EditorDescription;
    }


    public static COMMANDS ParsePart(string part)
    {
        foreach (COMMANDS cType in Enum.GetValues(typeof(COMMANDS)))
        {
            if (cType.ToString() == part)
            {
                return cType;
            }
        }
        return COMMANDS.NOCOMMAND;
    }
}


public class NoCommandsToExecuteException : Exception
{
    public NoCommandsToExecuteException(string msg) : base(msg)
    {

    }
}


public class FailedToParseException : Exception
{
    public FailedToParseException(string msg) : base(msg)
    {

    }
}


public class FailedToExecuteCommandException : Exception
{
    public FailedToExecuteCommandException(string msg) : base(msg)
    {

    }
}


public class ParsedCommands : List<List<(COMMANDS, string)>>
{

}


public class DoubleDict<TK, TV>
{
    private Dictionary<TK, TV> _oneWay = new Dictionary<TK, TV>();
    private Dictionary<TV, TK> _otherWay = new Dictionary<TV, TK>();

    public TV this[TK key]
    {
        get {return _oneWay[key];}
        set {_oneWay[key] = value;}
    }
    public TK this[TV reverseKey]
    {
        get {return _otherWay[reverseKey];}
        set {_otherWay[reverseKey] = value;}
    }

    public void Add(TK key, TV value)
    {
        _oneWay.Add(key, value);
        _otherWay.Add(value, key);
    }

    public void Remove(TK key)
    {
        TV value = this[key];
        _oneWay.Remove(key);
        _otherWay.Remove(value);
    }

    public void Remove(TV reverseKey)
    {
        TK reverseValue = this[reverseKey];
        _oneWay.Remove(reverseValue);
        _otherWay.Remove(reverseKey);
    }

    public TK[] Keys
    {
        get
        {
            TK[] keys = new TK[_oneWay.Keys.Count];
            _oneWay.Keys.CopyTo(keys, 0);
            return keys;
        }
    }

    public TV[] Values
    {
        get
        {
            TV[] values = new TV[_otherWay.Keys.Count];
            _otherWay.Keys.CopyTo(values, 0);
            return values;
        }
    }
}
