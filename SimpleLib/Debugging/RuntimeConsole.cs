using SimpleLib.GUI.sIMGUI;
using SimpleLib.Inputs;
using SimpleLib.Timing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TerraFX.Interop.Windows;

namespace SimpleLib.Debugging
{
    public static class RuntimeConsole
    {
        private static string _consoleInput = "";

        private static int _inputPosition = 0;
        private static int _suggestionFocus = 0;

        private static float _blink = 0.0f;

        private static List<ConsoleCommand> _commands = new List<ConsoleCommand>();

        private static List<int> _commandSuggestions = new List<int>();
        private static int _suggestionTextLength = 0;
        private static List<string> _currentSyntaxSuggestions = new List<string>();
        private static string _syntaxString = string.Empty;
        private static bool _isSuggestionVarType = false;
        private static Type? _currentSuggestionType = null;

        private static bool _isEnabled = false;
        private static bool _isDisplayed = false;

        static RuntimeConsole()
        {
            try
            {
#if DEBUG
                _isEnabled = true;
#else
                _isEnabled = File.Exists("devcon.enabled");
#endif
            }
            catch (Exception)
            {
            }
        }

        internal static void DrawToScreenViaIMGUI()
        {
            if (InputHandler.IsKeyRepeatedOrPressed(KeyCode.End))
            {
                _isDisplayed = !_isDisplayed;
            }

            if (!_isEnabled || !_isDisplayed)
            {
                return;
            }

            Vector2 screenSize = new Vector2(1336.0f, 726.0f);
            Span<ConsoleCommand> commands = CollectionsMarshal.AsSpan(_commands);

            sIMGUI.DrawList.AddRectFilled(new Vector2(0.0f, screenSize.Y - 24.0f), screenSize, new Vector4(0.1f, 0.1f, 0.1f, 1.0f));
            sIMGUI.DrawList.AddRectFilled(new Vector2(3.0f, screenSize.Y - 21.0f), screenSize - new Vector2(3.0f, 3.0f), new Vector4(0.15f, 0.15f, 0.15f, 1.0f));
            if (_consoleInput.Length == 0)
                sIMGUI.DrawList.AddText(new Vector2(6.0f, screenSize.Y - 8.0f), "Type command here...", new Vector4(0.4f, 0.4f, 0.4f, 1.0f));
            else
            {
                if (_syntaxString.Length > 0)
                {
                    Vector2 offset = sIMGUI.CalcTextSize(_consoleInput, _suggestionTextLength);
                    sIMGUI.DrawList.AddText(new Vector2(6.0f + offset.X, screenSize.Y - 8.0f), _syntaxString, new Vector4(0.4f, 0.4f, 0.4f, 1.0f));
                }
                else if (_commandSuggestions.Count > 0)
                {
                    sIMGUI.DrawList.AddText(new Vector2(6.0f, screenSize.Y - 8.0f), commands[_commandSuggestions[_suggestionFocus]].CommandName, new Vector4(0.4f, 0.4f, 0.4f, 1.0f));
                }

                sIMGUI.DrawList.AddText(new Vector2(6.0f, screenSize.Y - 8.0f), _consoleInput, Vector4.One);
            }

            Vector2 size = sIMGUI.CalcTextSize(_consoleInput, _inputPosition);
            if (_blink < 0.5f)
                sIMGUI.DrawList.AddRectFilled(new Vector2(6.0f + size.X, screenSize.Y - 8.0f), new Vector2(14.0f + size.X, screenSize.Y - 7.0f), Vector4.One);

            if (_currentSyntaxSuggestions.Count > 0)
            {
                int offset = Math.Max(0, _suggestionFocus - 10);
                int displayed = Math.Min(_currentSyntaxSuggestions.Count, 12);
                int displayedWithOffset = Math.Min(displayed + offset, _currentSyntaxSuggestions.Count);

                float width = 0.0f;
                for (int i = offset; i < displayedWithOffset; i++)
                {
                    width = Math.Max(width, sIMGUI.CalcTextSize(_currentSyntaxSuggestions[i]).X);
                }

                float h = (size.Y + 2.0f) * displayed + 33.0f;
                width += 14.0f;

                sIMGUI.DrawList.AddRectFilled(new Vector2(0.0f, screenSize.Y - h - 6.0f), new Vector2(width + 6.0f, screenSize.Y - 24.0f), new Vector4(0.1f, 0.1f, 0.1f, 1.0f));
                sIMGUI.DrawList.AddRectFilled(new Vector2(3.0f, screenSize.Y - h - 3.0f), new Vector2(width + 3.0f, screenSize.Y - 24.0f), new Vector4(0.15f, 0.15f, 0.15f, 1.0f));

                Vector4 color = Vector4.One;
                Vector4 dim = new Vector4(0.7f, 0.7f, 0.7f, 1.0f);

                if (_currentSuggestionType != null)
                {
                    if (_currentSuggestionType.IsEnum)
                    {
                        color = new Vector4(0.0f, 0.9f, 0.1f, 1.0f);
                        dim = new Vector4(0.0f, 0.5f, 0.1f, 1.0f);
                    }
                }

                //sIMGUI.ScreenCursor = new Vector2(6.0f, screenSize.Y - h + 6.0f + size.Y * 0.5f);
                sIMGUI.PushArea(new Vector2(6.0f, screenSize.Y - h + 6.0f + size.Y * 0.5f), new Vector2(width, h - 39.0f));
                for (int i = offset; i < displayedWithOffset; i++)
                {
                    sIMGUI.Text(_currentSyntaxSuggestions[i], _suggestionFocus == i ? color : dim);
                }
                sIMGUI.PopArea();
            }
            else if (_commandSuggestions.Count > 0)
            {
                float width = 0.0f;
                for (int i = 0; i < _commandSuggestions.Count; i++)
                {
                    ref ConsoleCommand cmd = ref commands[_commandSuggestions[i]];
                    width = Math.Max(width, sIMGUI.CalcTextSize(cmd.FullSyntax).X);
                }

                float h = (size.Y + 2.0f) * _commandSuggestions.Count + 30.0f;
                width += 6.0f;
                
                sIMGUI.DrawList.AddRectFilled(new Vector2(0.0f, screenSize.Y - h - 6.0f), new Vector2(width + 6.0f, screenSize.Y - 24.0f), new Vector4(0.1f, 0.1f, 0.1f, 1.0f));
                sIMGUI.DrawList.AddRectFilled(new Vector2(3.0f, screenSize.Y - h - 3.0f), new Vector2(width + 3.0f, screenSize.Y - 24.0f), new Vector4(0.15f, 0.15f, 0.15f, 1.0f));

                //sIMGUI.ScreenCursor = new Vector2(6.0f, screenSize.Y - h + 6.0f + size.Y * 0.5f);
                sIMGUI.PushArea(new Vector2(6.0f, screenSize.Y - h + 6.0f + size.Y * 0.5f), new Vector2(width, screenSize.Y));
                for (int i = 0; i < _commandSuggestions.Count; i++)
                {
                    ref ConsoleCommand cmd = ref commands[_commandSuggestions[i]];
                    sIMGUI.Text(cmd.FullSyntax, _suggestionFocus == i ? Vector4.One : new Vector4(0.7f, 0.7f, 0.7f, 1.0f));
                }
                sIMGUI.PopArea();
            }

            _blink = _blink > 1.0f ? 0.0f : _blink + FrameManager.DeltaTime;

            InputHandler.NeedsTextInputNextFrame = true;

            char? textInput = InputHandler.CharTextInput;
            if (textInput.HasValue)
            {
                _consoleInput = _consoleInput.Insert(_inputPosition++, textInput.Value.ToString());
                RecalculateSuggestions();
            }
            else if (_consoleInput.Length > 0 && _inputPosition > 0 && InputHandler.IsKeyRepeatedOrPressed(KeyCode.Backspace))
            {
                _consoleInput = _consoleInput.Remove(--_inputPosition, 1);
                RecalculateSuggestions();
            }
            else if (InputHandler.IsKeyPressed(KeyCode.Tab) && _consoleInput.Length > 0 && (_commandSuggestions.Count > 0 || (_isSuggestionVarType && _syntaxString.Length > 0)))
            {
                if ((_isSuggestionVarType && _syntaxString.Length > 0) || (_currentSuggestionType?.IsEnum ?? false))
                {
                    int find = (int)Math.Min((uint)_consoleInput.IndexOf(' ', Math.Min(_suggestionTextLength + 1, _consoleInput.Length)), (uint)_consoleInput.Length);
                    _consoleInput = _consoleInput.Remove(_suggestionTextLength, find - _suggestionTextLength).Insert(_suggestionTextLength, !_isSuggestionVarType ? _currentSyntaxSuggestions[_suggestionFocus] : _syntaxString);
                    _inputPosition = _suggestionTextLength + _syntaxString.Length;
                }
                else if (_syntaxString.Length == 0)
                {
                    _consoleInput = commands[_commandSuggestions[_suggestionFocus]].CommandName;
                    _inputPosition = _consoleInput.Length;
                    _suggestionFocus = 0;
                }

                RecalculateSuggestions();
            }

            if (InputHandler.IsKeyPressed(KeyCode.Return))
            {
                ProcessCommand();
                _consoleInput = string.Empty;
            }

            if (InputHandler.IsKeyRepeatedOrPressed(KeyCode.Left))
                _inputPosition--;
            if (InputHandler.IsKeyRepeatedOrPressed(KeyCode.Right))
                _inputPosition++;
            if (InputHandler.IsKeyRepeatedOrPressed(KeyCode.Up))
                _suggestionFocus--;
            if (InputHandler.IsKeyRepeatedOrPressed(KeyCode.Down))
                _suggestionFocus++;

            _inputPosition = Math.Clamp(_inputPosition, 0, _consoleInput.Length);
            _suggestionFocus = Math.Clamp(_suggestionFocus, 0, Math.Max(((_isSuggestionVarType || (_currentSuggestionType?.IsEnum ?? false)) ? _currentSyntaxSuggestions.Count : _commandSuggestions.Count) - 1, 0));
        }

        private static void RecalculateSuggestions()
        {
            _commandSuggestions.Clear();
            _currentSyntaxSuggestions.Clear();
            _suggestionTextLength = 0;
            _syntaxString = string.Empty;
            _isSuggestionVarType = false;
            _currentSuggestionType = null;

            if (_consoleInput.Length > 0)
            {
                string[] syntax = _consoleInput.Split(' ');

                if (syntax.Length > 0)
                {
                    Span<ConsoleCommand> commands = CollectionsMarshal.AsSpan(_commands);
                    for (int i = 0; i < commands.Length; i++)
                    {
                        ref ConsoleCommand command = ref commands[i];
                        if (command.CommandName.StartsWith(syntax[0]))
                        {
                            if (command.CommandName.Length == syntax[0].Length && syntax.Length > 1)
                            {
                                _suggestionTextLength += command.CommandName.Length + 1;

                                try
                                {
                                    int matches = 0;

                                    Span<CommandSyntax> cmdSyntaxes = command.Syntax.AsSpan();

                                    int limit = Math.Min(cmdSyntaxes.Length, syntax.Length - 1);
                                    for (int j = 0; j < limit; j++)
                                    {
                                        ref CommandSyntax cmdSyntax = ref cmdSyntaxes[j];
                                        if (cmdSyntax.TryParse(syntax[j + 1], out object? _))
                                        {
                                            matches++;
                                            _suggestionTextLength += syntax[j + 1].Length + 1;

                                            if (j + 2 == syntax.Length && matches != cmdSyntaxes.Length)
                                            {
                                                _commandSuggestions.Add(i);
                                            }
                                        }
                                        else if (j + 2 == syntax.Length || syntax[j + 1].Length == 0)
                                        {
                                            if (cmdSyntax.ObjType.IsEnum)
                                            {
                                                Array enums = Enum.GetValues(cmdSyntax.ObjType);

                                                bool match = syntax[j + 1].Length > 0;
                                                for (int k = 0; k < enums.Length; k++)
                                                {
                                                    string str = enums.GetValue(k)?.ToString() ?? string.Empty;
                                                    if (!match || (match && str.StartsWith(syntax[j + 1])))
                                                    {
                                                        _currentSyntaxSuggestions.Add(str);
                                                        if (match)
                                                        {
                                                            _syntaxString = str;
                                                            _isSuggestionVarType = true;
                                                        }
                                                    }
                                                }

                                                if (!match)
                                                {
                                                    _syntaxString = cmdSyntax.Suggestion;
                                                }
                                            }
                                            else
                                            {
                                                _syntaxString = cmdSyntax.Suggestion;
                                            }

                                            _currentSuggestionType = cmdSyntax.ObjType;
                                            _commandSuggestions.Add(i);
                                            break;
                                        }
                                    }

                                    if (matches == cmdSyntaxes.Length)
                                    {
                                        _commandSuggestions.Add(i);
                                    }
                                }
                                catch (Exception)
                                {
                                }
                            }
                            else
                            {
                                _commandSuggestions.Add(i);
                            }
                        }
                    }
                }
            }
        }

        private static void ProcessCommand()
        {
            try
            {
                ConsoleCommand command = _commands[_commandSuggestions[0]];
                object?[]? args = command.Syntax.Length > 0 ? new object?[command.Syntax.Length] : null;

                if (args != null)
                {
                    string[] syntax = _consoleInput.Split(' ');
                    for (int i = 0; i < command.Syntax.Length; i++)
                    {
                        CommandSyntax arg = command.Syntax[i];
                        if (syntax.Length - 1 <= i)
                        {
                            args[i] = arg.Default;
                        }
                        else
                        {
                            arg.TryParse(syntax[i + 1], out args[i]);
                        }
                    }
                }

                command.Method.Invoke(null, args);
            }
            catch (Exception ex)
            {
                LogTypes.Debug.Error(ex, "Failed to execute console command: \"{a}\"!", _consoleInput);
            }
        }

        public static bool Enabled { get => _isEnabled; set { _isEnabled = value; } }

        private readonly struct ConsoleCommand
        {
            public readonly string CommandName;
            public readonly CommandSyntax[] Syntax;

            public readonly MethodInfo Method;

            public readonly string FullSyntax;

            public ConsoleCommand(string name, CommandSyntax[] syntax, MethodInfo method)
            {
                CommandName = name;
                Syntax = syntax;

                Method = method;

                FullSyntax = name;
                for (int i = 0; i < syntax.Length; i++)
                {
                    CommandSyntax s = syntax[i];
                    if (s.Default != null)
                        FullSyntax += $" {s.Suggestion}";
                    else
                        FullSyntax += $" {s.Suggestion}";
                }
            }
        }

        private readonly struct CommandSyntax
        {
            public readonly string Name;
            public readonly Type ObjType;
            public readonly object? Default;

            public readonly string Suggestion;

            public CommandSyntax(string name, Type obj, object? defaultValue)
            {
                Name = name;
                ObjType = obj;
                Default = defaultValue;
                Suggestion = defaultValue == null ? $"<{name}:{obj.Name}>" : $"<{name}:{obj.Name}={defaultValue.ToString()}>";
            }

            public bool TryParse(string v, out object? o)
            {
                if (v.Length == 0)
                {
                    o = null;
                    return false;
                }

                if (ObjType.IsEnum)
                    return Enum.TryParse(ObjType, v, out o);
                else if (ObjType == typeof(string))
                {
                    if (v.Length > 1 && v.StartsWith('"') && v.EndsWith('"'))
                    {
                        if (v.Length > 2)
                            o = v.Substring(1, v.Length - 2);
                        else
                            o = string.Empty;
                        return true;
                    }
                }
                else if (ObjType.IsValueType || ObjType.IsGenericType || ObjType.IsPrimitive)
                {
                    o = Convert.ChangeType(v, ObjType);
                    return o != null;
                }

                o = null;
                return false;
            }
        }
    }
}
