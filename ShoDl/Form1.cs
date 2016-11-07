using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace ShoDl
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        /// <summary>
        /// For grabbing post ids and image links
        /// </summary>
        static readonly string QLink = "http://gelbooru.com/index.php?page=post&s=list&tags=wolf_tail";
        NQBalancer linkProcessor;
        NQBalancer imageProcessor;
        int max_pid=0;
        int cur_pid=0;
        int pIDCollPointer=0;
        HashSet<string> postIDCollection;
        HashSet<string> pictureLinkCollection;

        private void button1_Click(object sender, EventArgs e)
        {
            postIDCollection = new HashSet<string>();
            pictureLinkCollection = new HashSet<string>();

            pIDCollPointer = 0;
            linkProcessor = new NQBalancer(30);
            imageProcessor = new NQBalancer(10);// Max 3 images at a time
            //Grabbing first site
            linkProcessor.EnqeueCall(() => getPosts(QLink), ProcessPostCallback);

        }

        void ProcessPostCallback(HashSetIntColl<string> data)
        {
            if(InvokeRequired)
            {
                Invoke(new Action<HashSetIntColl<string>>(ProcessPostCallback), data);
                return;
            }
            if (data.val > max_pid)
                max_pid = data.val;
            //Start new post searching queries
            while (cur_pid <= max_pid )
            {  
                cur_pid += 42;
                int tmpi = cur_pid;
                linkProcessor.EnqeueCall(() => getPosts(QLink + "&pid=" + tmpi, max_pid), ProcessPostCallback);
            }
            foreach (var item in data.data)
            {
                postIDCollection.Add(item);
            }
            //Start new image searching queries
            for (; pIDCollPointer < postIDCollection.Count; pIDCollPointer++)
            {
                var tmpi = postIDCollection.ElementAt(pIDCollPointer);
                linkProcessor.EnqeueCall(() => getImg(tmpi) , getImgCallback);
            }
           

        }

        private HashSetIntColl<string> getPosts(string url,int maxPid=0)
        {
            WebClient wc = new WebClient() {Proxy=null};
            wc.Headers["Cache-Control"] = "max-age=0";
            wc.Headers["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
            wc.Headers["User-agent"] = "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:45.0) Gecko/20100101 Firefox/45.0";
            wc.Headers["Accept-Language"] = "en-US,en;q=0.8";
            wc.Headers["Cookie"] = "splashWeb-164278-42=1; _pdt=1; exo_zones=%7B%22banner%22:%5B%7B%22width%22:%22300%22,%22height%22:%22250%22,%22idzone%22:%221951884%22%7D,%7B%22width%22:%22300%22,%22height%22:%22250%22,%22idzone%22:%221680634%22%7D,%7B%22width%22:%22300%22,%22height%22:%22250%22,%22idzone%22:%221680634%22%7D%5D%7D";
            var tmpres = getMatchesPosts(wc.DownloadString(url), ref maxPid);
            return new HashSetIntColl<string> {val= maxPid, data= tmpres};
        }

        private HashSet<string> getImg(string item)
        {
            WebClient wc = new WebClient() { Proxy = null };
            HashSet<string> picLinks = new HashSet<string>();
            string pageSrc = wc.DownloadString("http://gelbooru.com/index.php?page=post&s=view&id=" + item);
            string pat = "href=\\\"(http:\\/\\/.*\\/\\/images.*.[A-Fa-f0-9]{32}\\.[a-zA-Z]{2,4})\\\"";
            MatchCollection pcount = Regex.Matches(pageSrc, pat, RegexOptions.IgnoreCase);
            foreach (Match match in pcount)
            {
                picLinks.Add((string)match.Groups[1].Value);
            }
            if (picLinks.Count == 0)
                Debugger.Break();

            return picLinks;
        }
        private void getImgCallback(HashSet<string> data) {
            if (InvokeRequired)
            {
                Invoke(new Action<HashSet<string>>(getImgCallback), data);
                return;
            }
       
            //foreach (var item in data)
            //{
            //    pictureLinkCollection.Add(item);
            //}
            //Starte das herunterladen
            foreach (var item in data)
            {
                var tmpuri = item;
                var turi = new Uri(tmpuri);
                if (System.IO.File.Exists(@"imgs\" + turi.Segments[turi.Segments.Length - 1]))
                    continue;
                imageProcessor.EnqeueCall(() => {
                  //  Debug.WriteLine("Downloading {0}", tmpuri);
                    if (!System.IO.Directory.Exists("imgs"))
                        System.IO.Directory.CreateDirectory("imgs");
                    WebClient wc = new WebClient() { Proxy = null };
                    wc.DownloadFile(tmpuri, @"imgs\" + turi.Segments[turi.Segments.Length - 1]);
                    return tmpuri;
                }, dlCallback);

            }

        }

        void dlCallback(string data)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string> (dlCallback), data);
                return;
            }
            string tmpstr = string.Format("Last status: {0}\n",data);
            int linkFindrStuff = 0;
            int dlQuery=0;
            for (int i = 0; i < linkProcessor.Pool.Count; i++)
            {
                linkFindrStuff += linkProcessor.Pool[i].processQueue.Count;
            }
            for (int i = 0; i < imageProcessor.Pool.Count; i++)
            {
                dlQuery += imageProcessor.Pool[i].processQueue.Count;
            }
            tmpstr += string.Format("Pages2Index: {0}\n", linkFindrStuff);
            tmpstr += string.Format("Images2Dl: {0}\n", dlQuery);
            label1.Text = tmpstr;

        }


        class HashSetIntColl<T>
        {
            public HashSet<T> data { get; set; }
            public int val { get; set; }
        }


        private List<string> getImgPosts(string url) {
        

            WebClient wc = new WebClient();
            wc.Proxy = null;

            HashSet<string> postIDs = new HashSet<string>();
            HashSet<string> picLinks = new HashSet<string>();



            //Get all Posts id
            int maxPid = 0;
            int curPid = 0;
            for (int i = 0; i <= maxPid ; i+=42)
            {
                getMatchesPosts(wc.DownloadString(url + "&pid=" + i.ToString()),ref maxPid, postIDs);
                //Starting jobs For new ones (Grabbing picture links)
            
                curPid = postIDs.Count - 1;
            }
            //Get all picture links
            foreach (var item in postIDs)
                {
                    string pageSrc = wc.DownloadString("http://gelbooru.com/index.php?page=post&s=view&id="+item);
                    string pat = "href=\\\"(http:\\/\\/.*\\/\\/images.*.[A-Fa-f0-9]{32}\\.[a-zA-Z]{2,4})\\\"";
                    MatchCollection pcount = Regex.Matches(pageSrc, pat, RegexOptions.IgnoreCase);        
                    foreach (Match match in pcount)
                    {
                        picLinks.Add((string)match.Groups[1].Value);
                    }
                }
            //Dl items

            return null;
        }
        private HashSet<string> getMatchesPosts(string input, ref int maxPid, HashSet<string> uretList = null)
        {
            if (uretList == null)
                uretList = new HashSet<string>();

            string pat = ".[a-zA-Z]{2,4}\\?(\\d+)\\\"";
            //
            MatchCollection pcount = Regex.Matches(input, pat, RegexOptions.IgnoreCase);
            foreach (Match match in pcount)
            {
                uretList.Add((string)match.Groups[1].Value);
            }

            string mpat = "id=(\\d+)\\\" alt=\\\"last";
            MatchCollection mcount = Regex.Matches(input, mpat, RegexOptions.IgnoreCase);
            foreach (Match match in mcount)
            {
                int tmpmax;
                if(Int32.TryParse(match.Groups[1].Value,out tmpmax))
                    if (tmpmax > maxPid)
                        maxPid = tmpmax;
            }
            return uretList;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
   

        }

        private void button2_Click(object sender, EventArgs e)
        {
            var KK = getImg("3413222");
            Debugger.Break();
        }
    }
}
