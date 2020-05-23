using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MeCab;
using MeCab.Extension.IpaDic;
using MeCab.Extension.UniDic;

namespace RomanizationTesting
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = UTF8Encoding.UTF8;
            
            string path = @"F:\Files\Documents\SmartLyrics\Testing";
            string toRomanize = File.ReadAllText(path + @"\original.txt");
            Console.WriteLine("Read from file");
            Console.Write(toRomanize + "\n\n");

            MeCabParam parameter = new MeCabParam();
            MeCabTagger tagger = MeCabTagger.Create(parameter);

            Console.WriteLine("Parsing...");
            //foreach (var node in tagger.ParseToNodes(toRomanize))
            //{
            //    Console.Write(node.GetKanaBase());
            //}

            Console.Write(tagger.ParseToNode(toRomanize).GetPronounciation());

            Console.WriteLine("Finished!");

            Console.ReadLine();
        }
    }
}
