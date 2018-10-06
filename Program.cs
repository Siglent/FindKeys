/*
FindKeys - a .NET Core 2.1 utility  
  
Derived from a code sample posted by 'tv84' on eevblog.  
See: https:www.eevblog.com/forum/testgear/siglent-ads-firmware-file-format/msg1774208/#msg1774208  
  
The purpose of this utility is to recover your valid keys you licensed with your scope but you lost  
the paperwork for. First you must obtain a memory dump from your scope. Follow the procedures documented  
in various posts on eevblog.  
  
Upon startup, the program reads the contents of "FindKeys.json" to configure various parameters needed  
for it to work. The options in this file and their purpose are as follows:  
  
binfile: The fully-qualified path to the memory dump you wish to scan, e.g. "g:memdump.bin"  
  
keyfile: The fully-qualified path to the file the list of keys will be written to, e.g. "g:trykeys.txt"  
  
family: the product family you are scanning for. Currently supported product families include:  
        SDS1X-E  - the SDS1###X-E family of oscilloscopes  
  
checkfile: (optional) a file containing a key/option pair to check against once the scan is correct.  
           this option is used to verify the keys are, in fact, in the memory dump.  
  

Theory of operation:

License keys are 16 printable ascii characters in length. This program scans through a memory dump binary  
file and, each time it encounters string of a length divisible by 16, it processes it as a series of  
possible keys and stores the values in a SortedSet. 
  
Since memory dumps are not of contiguous memory, the program also monitors when it encounters a 4K memory  
page boundary, and, if it has a "partial key" of 4, 8 or 12 characters at either the end or beginning of  
a memory page, it will add that partial string a temporary holding location. Once the entire memory scan  
is complete, all of the 4 and 12 character partial strings, and all the 8 character partial strings, are  
concatinated together to form additional key possibilities (since, for example, 4 characters of a key  
may have been at the end of one memory page, and the remaining 12 characters somewhere else at the  
beginning of a memory page.)  

During execution you will see a line:

Scanning for keys: 0059 0005 0007 0023

This tells you how many full 16-character keys, and how many 12, 8, and 4 character "chunks" have  
been detected by the program thusfar.  
  
Once all the keys possibilities are generated, the program will optionally check them against a file  
containing a list of known option/key pairs (i.e. "AWG xxxyyywwwzzzaaa1" or "MSO ccccffffggggdddd"),  
one option/key pair per line, to determine if they key was found. This optional mechanism exists as a  
check for the developers to ensure their code was working :) but we left it in so others who wanted  
to try the program on their own could see if the program detected their license keys without needing  
to sift through all the possibilities generated and stored in the output keyfile.  

To execute from the command line:   dotnet FindKeys.dll  

Sample log file:

Execution starts @ 10/6/2018 8:52 AM          
Scanning for keys: 0059 0005 0007 0023          
Found 200M option: 2222222222222222  
Found 100M option: 1111111111111111           
Found 70M option: 7777777777777777  
Found 50M option: 5555555555555555              
Found AWG option: AAAAAAAAAAAAAAAA  
Found MSO option: MMMMMMMMMMMMMMMM  
Found WIFI option: WWWWWWWWWWWWWWWW  

320 possible keys written to g:keyfile.txt

Execution ends @ 10/6/2018 8:53 AM

This program has dependencies you must install through the NuGet Package Manager.  
They are: Microsoft.Extensions.Configuration.  
  
Note: at the moment this utility only supports the SDS####X-E series of scopes, however, additional  
functionality will be added as details become available.  

Special thanks to eevblog user tv84 who gave me tons of assistance during the development of this  
utility. His (or her - you never know on the Internet!) constant feedback prompted me to continuously  
hone and refine this program to make it better.
*/
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace FindKeys
{
    static class Program
    {
        static SortedSet<String> keys = new SortedSet<String>();

        static List<String> parts4 = new List<String>();
        static List<String> parts8 = new List<String>();
        static List<String> parts12 = new List<String>();

        static Boolean b_AllNumeric = false;
        static Boolean b_AllUpper = true;
        static Boolean b_AllLower = false;

        static void Main(string[] args)
        {
            Boolean b;
            Int32 i = 0, j = 0, l = 0, x = 0, y = 0, strStart = 0, strSize = 0;
            String s = String.Empty, t = String.Empty;
            Char c;

            IConfigurationRoot config = new ConfigurationBuilder().AddJsonFile("FindKeys.json", false, true).Build();

            if (!String.IsNullOrEmpty(config["binfile"]))
            {
                if (!File.Exists(config["binfile"]))
                {
                    Console.WriteLine("ERROR: Binary file not found (wrong file specified in configuration file?)");
                    return;
                }
            }
            else
            {
                Console.WriteLine("ERROR: Binary file not specified in configuration file.");
                return;
            }

            if (!String.IsNullOrEmpty(config["keyfile"]))
            {
                if (File.Exists(config["keyfile"]))
                {
                    Console.WriteLine("WARNING: Key file exists and will be overwritten.");
                }
                else
                {
                    if (!CheckValidPath(config["keyfile"]))
                    {
                        Console.WriteLine("ERROR: Output filename is not valid or path does not exist.");
                        return;
                    }
                }
            }
            else
            {
                Console.WriteLine("ERROR: Key file not specified in configuration file.");
                return;
            }

            if (!String.IsNullOrEmpty(config["family"]))
            {
                switch (config["family"].ToUpper())
                {
                    case "SDS1X-E":
                        b_AllLower = false;
                        b_AllUpper = true;
                        b_AllNumeric = false;
                        break;
                    default:
                        Console.WriteLine("ERROR: Unsupported scope family specified in configuration file.");
                        return;
                }
            }
            else
            {
                Console.WriteLine("ERROR: Scope family not specified in configuration file.");
                return;
            }

            Console.WriteLine("Execution starts @ " + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString());

            byte[] buffer = File.ReadAllBytes(config["binfile"]);

            Console.Write("Scanning for keys: 0000 0000 0000 0000");

            for (j = 0, l = 0; j < 2; j++, l += 0x20)
            {
                for (i = 0, strStart = 0, strSize = 0; i < buffer.Length; i++)
                {
                    c = (Char)buffer[i];
                    if (((c < '2') || (c > '9')) && ((c < 'A' + l) || (c > 'Z' + l)) && c != ('L' + l) && c != ('O' + l))
                    {
                        b = (strStart % 4096 == 0) || (i % 4096 == 0);
                        if (strSize > 15 || (strSize > 3 && b ))
                        {
                            // handles:
                            //    /--- key #1 ---\/--- key #2 ---\
                            //    wwwwwwwwwwwwwwwwxxxxxxxxxxxxxxxx
                            //
                            if (strSize % 16 == 0)
                            {
                                s = Encoding.UTF8.GetString(buffer, strStart, strSize);
                                while (s.Length > 15)
                                    CheckAndAdd(PeelOffString(ref s, 16));
                            }
                            //
                            // handles any of these four scenarios:
                            //
                            // /--- key #1 ---\/- key #n -\              or      /--- key #1 ---\/- key #n -\
                            // xxxxxxxxxxxxxxxx[...keys...]ssssssssssss      ppppxxxxxxxxxxxxxxxx[...keys...]ssssssss
                            //
                            //         /--- key #1 ---\/- key #n -\      or              / ---key #1 ---\/- key #n -\
                            // ppppppppxxxxxxxxxxxxxxxx[...keys...]ssss      ppppppppppppxxxxxxxxxxxxxxxx[...keys...]
                            //
                            if (strSize % 4 == 0 && b)
                            {
                                // 0, 4, 8, 12 byte offsets
                                for (x = 0; x < 16; x = x + 4)
                                {
                                    s = Encoding.UTF8.GetString(buffer, strStart, strSize);
                                    // deal with "prefix", which could be partial key possibilities
                                    CheckAndAdd(PeelOffString(ref s, x));
                                    // deal with centers, which are whole key possibilities
                                    while (s.Length > 15)
                                        CheckAndAdd(PeelOffString(ref s, 16));
                                    // deal with suffix, which could be partial key possibility
                                    CheckAndAdd(s);
                                }
                            }
                        }
                        strSize = 0;
                        strStart = i + 1;
                    }
                    else
                        strSize++;
                }
            }

            Console.WriteLine();

            keys.UnionWith(ConsolidateParts(parts8, parts8));
            keys.UnionWith(ConsolidateParts(parts4, parts12));
            keys.UnionWith(ConsolidateParts(parts12, parts4));

            if (keys.Count > 0)
            {
                StreamWriter sw = new StreamWriter(config["keyfile"], false);
                foreach (String u in keys)
                {
                    sw.WriteLine(u);
                }
                sw.Close();
            }

            if (!String.IsNullOrEmpty(config["checkfile"]))
            {
                if (File.Exists(config["checkfile"]))
                {
                    String[] fields;
                    StreamReader file = new StreamReader(config["checkfile"]);
                    while ((s = file.ReadLine()) != null)
                    {
                        fields = s.Trim().Split(' ');
                        if (fields.Length > 1 && keys.Contains(fields[1]))
                            Console.WriteLine("Found {0} option: {1}", fields[0], fields[1]);
                    }
                    file.Close();
                }
            }

            Console.WriteLine("\n{0} possible keys written to {1}", keys.Count, config["keyfile"]);
            Console.WriteLine("\nExecution ends @ " + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString());

        }

        static String PeelOffString(ref String s, Int32 i)
        {
            String rc = "";
            if (s.Length >= i)
            {
                rc = s.Substring(0, i);
                s = s.Remove(0, i);
            }
            return rc;
        }

        static SortedSet<String> ConsolidateParts(List<String> p1, List<String> p2)
        {
            SortedSet<String> rc = new SortedSet<String>();
            String s = String.Empty;
            Int32 i = 0, j = 0;
            for (i = 0; i < p1.Count; i++)
            {
                for (j = 0; j < p2.Count; j++)
                {
                    if (i != j)
                    {
                        s = p1[i] + p2[j];
                        if (!s.isMixedCase())
                        {
                            rc.Add(s);
                        }
                    }
                }
            }
            return rc;
        }

        static Boolean CheckAndAdd(String s)
        {
            Boolean b_ok = ((s.isLowerCase() && b_AllLower) ||
                            (s.isUpperCase() && b_AllUpper) ||
                            (s.isNumeric() && b_AllNumeric) &&
                            s.Length % 4 == 0 && s.Length > 0);
            if (b_ok)
            {
                switch (s.Length)
                {
                    case 4:
                        if (!parts4.Contains(s))
                            parts4.Add(s);
                        break;
                    case 8:
                        if (!parts8.Contains(s))
                            parts8.Add(s);
                        break;
                    case 12:
                        if (!parts12.Contains(s))
                            parts12.Add(s);
                        break;
                    case 16:
                        if (!keys.Contains(s))
                            keys.Add(s);
                        break;
                }
                Console.Write("{0}{1:D4} {2:D4} {3:D4} {4:D4}", new String('\b', 19), keys.Count, parts12.Count, parts8.Count, parts4.Count);
            }
            return b_ok;
        }
        private static Boolean CheckValidPath(String sFileName)
        {
            FileInfo fi = null;
            try
            {
                fi = new FileInfo(sFileName);
            }
            catch (ArgumentException) { }
            catch (System.IO.PathTooLongException) { }
            catch (NotSupportedException) { }
            return (ReferenceEquals(fi, null)) ? false : Directory.Exists(fi.DirectoryName);
        }
    }
}