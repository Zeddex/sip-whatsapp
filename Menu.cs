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
                    .Required()
                    .InstructionsText(
                        "[grey]Press [blue]<space>[/] to toggle a codec, " +
                        "[green]<enter>[/] to accept[/]")
                    .AddChoices(codecsList));

            List<AudioCodecsEnum> parsedEnums = Ext.EnumParse<AudioCodecsEnum>(codecs);

            return parsedEnums;
        }

        public string StunServer()
        {
            List<string> serversList = new List<string>()
            {
                "stun.l.google.com:19302",
                "stun1.l.google.com:19302",
                "stun2.l.google.com:19302",
                "stun3.l.google.com:19302",
                "stun4.l.google.com:19302",
                "stun.ekiga.net",
                "stun.ideasip.com",
                "stun.schlund.de",
                "stun.stunprotocol.org:3478",
                "stun.voiparound.com",
                "stun.voipbuster.com",
                "stun.voipstunt.com",
                "stun.voxgratia.org"
            };

            var server = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select STUN server:")
                    .PageSize(10)
                    .MoreChoicesText("[grey](Move up and down to reveal more servers)[/]")
                    .AddChoices(serversList)
                    );

            return server;
        }
    }
}