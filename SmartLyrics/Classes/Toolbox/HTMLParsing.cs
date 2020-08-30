using HtmlAgilityPack;
using static SmartLyrics.Common.Logging;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SmartLyrics.Toolbox
{
    internal class HTMLParsing
    {
        //takes a Genius song page (old and 2020 design) and returns a HTML formatted
        //string containing lyrics
        public static async Task<string> ParseHTML(HtmlDocument doc)
        {
            if (doc.Text.Contains("<div class=\"lyrics\">"))
            {
                //EX: Handle NullReferenceException from error pages and such
                Log(LogPriority.Warn, "Song page uses old design");
                HtmlNode node = doc.DocumentNode.SelectSingleNode("//div[@class='lyrics']");
                string output = await CleanHTML(node.InnerHtml);
                return output;
            }
            else
            {
                HtmlNode[] nodes = doc.DocumentNode.SelectNodes("//div[@class[contains(., 'Lyrics__Container-')]]").ToArray();
                string finalLyrics = "";
                foreach (HtmlNode n in nodes)
                {
                    finalLyrics += System.Net.WebUtility.HtmlDecode(n.InnerHtml);
                }

                string output = await CleanHTML(finalLyrics);
                return output;
            }
        }

        private static async Task<string> CleanHTML(string input)
        {
            //replace <, > and </ in HTML italic and bold tags into |\, ||, |/ respectivly to prevent
            //their deletion when using the regex.
            string _ = Regex.Replace(input.Trim(), @"\t|\n|\r", "");

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

            Log(Type.Info, "Cleaned HTML");

            string output = _;
            return output;
        }
    }
}