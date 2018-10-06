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

Sample log files (with and without check file):

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