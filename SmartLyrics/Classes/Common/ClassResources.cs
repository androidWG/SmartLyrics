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
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public string FeaturedArtist { get; set; }
        public string Cover { get; set; }
        public string Header { get; set; }
        public string APIPath { get; set; }
        public string Path { get; set; }
        public string Lyrics { get; set; }
        public int Likeness { get; set; } //used by the NLService
        public int Id { get; set; }
    }

    public class Artist
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<Song> Songs { get; set; }
    }

    public class Lyrics
    {
        public int Id { get; set; }
        public string Lyric { get; set; }
    }
    #endregion

    #region Adapters
    public class ExpandableListAdapter : BaseExpandableListAdapter
    {
        private readonly Activity context;
        private readonly List<string> listDataHeader;
        private readonly Dictionary<string, List<Song>> listDataChild;
        //private readonly List<string> filteredHeader;
        //private readonly Dictionary<string, List<string>> filteredChild;

        public ExpandableListAdapter(Activity context, List<string> listDataHeader, Dictionary<string, List<Common.Song>> listChildData)
        {
            this.listDataChild = listChildData;
            this.listDataHeader = listDataHeader;
            this.context = context;
        }
        //for child item view
        public override Java.Lang.Object GetChild(int groupPosition, int childPosition)
        {
            return listDataChild[listDataHeader[groupPosition]][childPosition].Title;
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
        private readonly Activity activity;
        private readonly List<Tuple<string, Song>> allSongs;

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

            titleTxt.Text = allSongs.ElementAt(position).Item2.Title;
            artistTxt.Text = allSongs.ElementAt(position).Item1;
            return view;
        }
    }

    public class SearchResultAdapter : BaseAdapter<Common.Song>
    {
        private readonly Activity activity;
        private readonly List<Song> songs;

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
            return songs[position].Id;
        }

        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            View view = convertView ?? activity.LayoutInflater.Inflate(Resource.Layout.list_item, parent, false);
            TextView titleTxt = view.FindViewById<TextView>(Resource.Id.songTitle);
            TextView artistTxt = view.FindViewById<TextView>(Resource.Id.songArtist);
            ImageView coverImg = view.FindViewById<ImageView>(Resource.Id.cover);

            titleTxt.Text = songs[position].Title;
            artistTxt.Text = songs[position].Artist;
            ImageService.Instance.LoadUrl(songs[position].Cover).Transform(new RoundedTransformation(20)).Into(coverImg);

            return view;
        }
    }
    #endregion
}