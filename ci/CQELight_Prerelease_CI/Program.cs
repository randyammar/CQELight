﻿using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CQELight_Prerelease_CI
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length == 0 || !Directory.Exists(args[0]))
            {
                Environment.Exit(-1);
            }
            var csprojPath = Path.Combine(args[0], "Geneao.csproj");
            if (!File.Exists(csprojPath))
            {
                Environment.Exit(-1);
            }
            var famillesJson = Path.Combine(args[0], "familles.json");
            var eventsDb = Path.Combine(args[0], "events.db");
            if (File.Exists(famillesJson))
            {
                File.Delete(famillesJson);
            }
            if (File.Exists(eventsDb))
            {
                File.Delete(eventsDb);
            }

            var processInfos = new ProcessStartInfo("dotnet", $"run {csprojPath}");
            processInfos.WorkingDirectory = args[0];
            processInfos.RedirectStandardOutput = true;
            processInfos.RedirectStandardInput = true;
            processInfos.RedirectStandardError = true;
            processInfos.CreateNoWindow = true;
            processInfos.UseShellExecute = false;

            bool created = false;
            bool listed = false;
            bool personCreated = false;
            bool creation = false;
            bool listing = false;
            bool personCreation = false;

            StringBuilder sb = new StringBuilder();

            var process = Process.Start(processInfos);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.ErrorDataReceived += (s, e) =>
            {
                sb.Append("**ERROR **").AppendLine(e.Data);
                process.Kill();
            };
            process.OutputDataReceived += (s, e) =>
            {
                if (e?.Data != null)
                {
                    sb.AppendLine(e.Data);
                    if (string.IsNullOrEmpty(e.Data) && !creation && !listing && !personCreation)
                    {
                        if (!created)
                        {
                            //Test creation
                            process.StandardInput.WriteLine("2");
                            creation = true;
                        }
                        else if (!personCreated)
                        {
                            process.StandardInput.WriteLine("3");
                            personCreation = true;
                        }
                        else
                        {
                            process.StandardInput.WriteLine("1");
                            listing = true;
                        }
                    }
                    else if (e.Data.Contains("Choisissez un nom de famille pour la créer"))
                    {
                        process.StandardInput.WriteLine("Test");
                    }
                    else if (e.Data.Contains("Veuillez entrer le nom de la personne à créer"))
                    {
                        process.StandardInput.WriteLine("John");
                    }
                    else if (e.Data.Contains("Veuillez entrer le lieu de naissance de la personne à créer"))
                    {
                        process.StandardInput.WriteLine("Paris");
                    }
                    else if (e.Data.Contains("Veuillez entrer la date de naissance (dd/MM/yyyy)"))
                    {
                        process.StandardInput.WriteLine("25/01/1976");
                    }
                    else if (e.Data.Contains("La famille Test a correctement été créée dans le système"))
                    {
                        created = true;
                        creation = false;
                    }
                    else if (e.Data.Contains("John a correctement été ajouté(e) à la famille Test."))
                    {
                        personCreated = true;
                        personCreation = false;
                    }
                    else if (e.Data.Contains("Veuillez saisir la famille concernée"))
                    {
                        process.StandardInput.WriteLine("Test");
                    }
                    else if (e.Data == "Test") // Listing
                    {
                        listing = false;
                        listed = true;
                        process.Kill();
                        Console.WriteLine("Everything went fine");
                        Environment.Exit(0);
                    }
                }
            };
            int awaitedTime = 0;
            while(awaitedTime < 180000)
            {
                if (created && listed && personCreated) break;
                await Task.Delay(200);
                awaitedTime += 200;
            }
            process.Kill();
            var exitCode = created && listed && personCreated ? 0 : -1;
            if (exitCode != 0)
            {
                Console.WriteLine("Test failed. Transcription below");
                Console.WriteLine(sb.ToString());
            }
            else
            {
                Console.WriteLine("Everything went fine");
            }
            Environment.Exit(exitCode);
        }
    }
}
