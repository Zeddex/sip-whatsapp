using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spectre.Console;
using SIPSorceryMedia.Abstractions;

namespace SipWA
{
    internal class Menu
    {
        public void StartInfo()
        {
            AnsiConsole.MarkupLine("[bold blue]SIP - WhatsApp[/]\n");
        }

        public List<AudioCodecsEnum> CodecsList()
        {
            List<string> codecsList = Enum.GetValues(typeof(AudioCodecsEnum))
                .Cast<AudioCodecsEnum>()
                .Select(v => v.ToString())
                .ToList();

            var codecs = AnsiConsole.Prompt(
                new MultiSelectionPrompt<string>()
                    .Title("Select codecs:")
                    .PageSize(20)
                    .InstructionsText(
                        "[grey]Press [blue]<space>[/] to toggle a codec, " +
                        "[green]<enter>[/] to accept[/]")
                    .AddChoices(codecsList));

            List<AudioCodecsEnum> parsedEnums = Ext.EnumParse<AudioCodecsEnum>(codecs);

            return parsedEnums;
        }
    }
}
