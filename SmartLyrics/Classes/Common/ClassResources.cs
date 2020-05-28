using Android.App;
using Android.Views;
using Android.Widget;

using FFImageLoading;
using FFImageLoading.Transformations;

using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartLyrics.Common
{
    #region Storage Classes or just classes idk what to call this
    public class Song
    {
        public string title { get; set; }
        public string artist { get; set; }
        public string album { get; set; }
        public string featuredArtist { get; set; }
        public string cover { get; set; }
        public string header { get; set; }
        public string APIPath { get; set; }
        public string path { get; set; }
        public string lyrics { get; set; }
        public int likeness { get; set; } //used by the NLService
        public int id { get; set; }
    }

    public class Artist
    {
        public int id { get; set; }
        public string name { get; set; }
        public List<Song> songs { get; set; }
    }

    public class Lyrics
    {
        public int id { get; set; }
        public string lyrics { get; set; }
    }
    #endregion

    #region Adapters
    public class ExpandableListAdapter : BaseExpandableListAdapter
    {
        private Activity context;
        private List<string> listDataHeader;
        private Dictionary<string, List<Song>> listDataChild;
        private List<string> filteredHeader;
        private Dictionary<string, List<string>> filteredChild;

        public ExpandableListAdapter(Activity context, List<string> listDataHeader, Dictionary<string, List<Common.Song>> listChildData)
        {
            this.listDataChild = listChildData;
            this.listDataHeader = listDataHeader;
            this.context = context;
        }
        //for child item view
        public override Java.Lang.Object GetChild(int groupPosition, int childPosition)
        {
            return listDataChild[listDataHeader[groupPosition]][childPosition].title;
        }
        public override long GetChildId(int groupPosition, int childPosition)
        {
            return childPosition;
        }

        public override View GetChildView(int groupPosition, int childPosition, bool isLastChild, View convertView, ViewGroup parent)
        {
            string childText = (string)GetChild(groupPosition, childPosition);
            if (convertView == null)
            {
                convertView = context.LayoutInflater.Inflate(Resource.Layout.list_child, null);
            }
            TextView txtListChild = (TextView)convertView.FindViewById(Resource.Id.listChild);
            txtListChild.Text = childText;
            return convertView;
        }
        public override int GetChildrenCount(int groupPosition)
        {
            return listDataChild[listDataHeader[groupPosition]].Count;
        }
        //For header view
        public override Java.Lang.Object GetGroup(int groupPosition)
        {
            return listDataHeader[groupPosition];
        }
        public override int GroupCount {
            get {
                return listDataHeader.Count;
            }
        }
        public override long GetGroupId(int groupPosition)
        {
            return groupPosition;
        }
        public override View GetGroupView(int groupPosition, bool isExpanded, View convertView, ViewGroup parent)
        {
            string headerTitle = (string)GetGroup(groupPosition);

            convertView = convertView ?? context.LayoutInflater.Inflate(Resource.Layout.list_header, null);
            TextView lblListHeader = (TextView)convertView.FindViewById(Resource.Id.listHeader);
            lblListHeader.Text = headerTitle;

            return convertView;
        }
        public override bool HasStableIds {
            get {
                return false;
            }
        }

        public override bool IsChildSelectable(int groupPosition, int childPosition)
        {
            return true;
        }
    }

    public class SavedLyricsAdapter : BaseAdapter<Artist>
    {
        private Activity activity;
        List<Tuple<string, Song>> allSongs;

        public override Artist this[int position] => throw new NotImplementedException();

        public SavedLyricsAdapter(Activity activity, List<Tuple<string, Common.Song>> allSongs)
        {
            this.activity = activity;
            this.allSongs = allSongs;
        }

        public override int Count {
            get { return allSongs.Count; }
        }

        public override long GetItemId(int position)
        {
            return position;
        }

        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            View view = convertView ?? activity.LayoutInflater.Inflate(Resource.Layout.list_non_grouped, parent, false);
            TextView titleTxt = view.FindViewById<TextView>(Resource.Id.songTitle);
            TextView artistTxt = view.FindViewById<TextView>(Resource.Id.songArtist);

            titleTxt.Text = allSongs.ElementAt(position).Item2.title;
            artistTxt.Text = allSongs.ElementAt(position).Item1;
            return view;
        }
    }

    public class SearchResultAdapter : BaseAdapter<Common.Song>
    {
        private Activity activity;
        private List<Song> songs;

        public override Song this[int position] => throw new NotImplementedException();

        public SearchResultAdapter(Activity activity, List<Common.Song> songs)
        {
            this.activity = activity;
            this.songs = songs;
        }

        public override int Count {
            get { return songs.Count; }
        }

        public override long GetItemId(int position)
        {
            return songs[position].id;
        }

        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            View view = convertView ?? activity.LayoutInflater.Inflate(Resource.Layout.list_item, parent, false);
            TextView titleTxt = view.FindViewById<TextView>(Resource.Id.songTitle);
            TextView artistTxt = view.FindViewById<TextView>(Resource.Id.songArtist);
            ImageView coverImg = view.FindViewById<ImageView>(Resource.Id.cover);

            titleTxt.Text = songs[position].title;
            artistTxt.Text = songs[position].artist;
            ImageService.Instance.LoadUrl(songs[position].cover).Transform(new RoundedTransformation(20)).Into(coverImg);

            return view;
        }
    }
    #endregion
}