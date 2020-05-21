using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using HtmlAgilityPack;
using OYMLCN;

namespace TestingGrounds
{
    class Program
    {
        static void Main(string[] args)
        {
            HtmlWeb web = new HtmlWeb();

            while (true)
            {
                Console.WriteLine("Paste webpage to load");
                string webpage = Console.ReadLine();

                HtmlDocument doc = web.Load(webpage);
                Console.WriteLine("Loaded webpage...\n");

                if (doc.Text.Contains("<div class=\"lyrics\">"))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Page uses old design\n");
                    Console.ForegroundColor = ConsoleColor.White;

                    HtmlNode node = doc.DocumentNode.SelectSingleNode("//div[@class='lyrics']");
                    File.WriteAllText(@"F:\Files\Downloads\rapgod.html", CleanHTML(node.InnerHtml));
                    Console.WriteLine("Wrote to file.");
                }
                else
                {
                    HtmlNode[] nodes = doc.DocumentNode.SelectNodes("//div[@class[contains(., 'Lyrics__Container-')]]").ToArray();
                    string finalLyrics = "";
                    foreach (HtmlNode n in nodes)
                    {
                        finalLyrics += System.Net.WebUtility.HtmlDecode(n.InnerHtml);
                    }

                    File.WriteAllText(@"F:\Files\Downloads\rapgod.html", CleanHTML(finalLyrics));
                    Console.WriteLine("Wrote to file.");
                }
            }
        }

        static string CleanHTML(string input)
        {
            //replace <, > and </ in HTML italic and bold tags into |\, ||, |/ respectivly to prevent
            //their deletion when using the regex.
            string _ = Regex.Replace(input.Trim(), @"\t|\n|\r", "");
                                    //^^ also trim it just for perfectionism sake

            _ = _.Replace("<b>", @"|\b||");
            _ = _.Replace("</b>", @"|/b||");
            _ = _.Replace("<i>", @"|\i||");
            _ = _.Replace("</i>", @"|/i||");
            _ = _.Replace("<br>", @"|\br||");
            _ = _.Replace("</div>", @"|\br||");

            //remove anything between < and >, like link and useless HTML stuff
            _ = Regex.Replace(_, "<.*?>", "");

            _ = _.Replace(@"|\", "<");
            _ = _.Replace(@"||", ">");
            _ = _.Replace(@"|/", "</");

            _ = _.Replace("<br><br><br>", "<br><br>");

            string output = _;
            return output;
        }
    }
}