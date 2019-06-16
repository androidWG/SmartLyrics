using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Graphics;

namespace SmartLyrics
{
    public class Extensions
    {
        public class Artist
        {
            public string id { get; set; }
            public string name { get; set; }
            public List<string> songs { get; set; }
        }

        public class ExpandableListAdapter : BaseExpandableListAdapter
        {
            private Activity _context;
            private List<string> _listDataHeader; // header titles
                                                  // child data in format of header title, child title
            private Dictionary<string, List<string>> _listDataChild;

            public ExpandableListAdapter(Activity context, List<string> listDataHeader, Dictionary<String, List<string>> listChildData)
            {
                _context = context;
                _listDataHeader = listDataHeader;
                _listDataChild = listChildData;
            }
            //for child item view
            public override Java.Lang.Object GetChild(int groupPosition, int childPosition)
            {
                return _listDataChild[_listDataHeader[groupPosition]][childPosition];
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
                    convertView = _context.LayoutInflater.Inflate(Resource.Layout.list_child, null);
                }
                TextView txtListChild = (TextView)convertView.FindViewById(Resource.Id.listChild);
                txtListChild.Text = childText;
                return convertView;
            }
            public override int GetChildrenCount(int groupPosition)
            {
                return _listDataChild[_listDataHeader[groupPosition]].Count;
            }
            //For header view
            public override Java.Lang.Object GetGroup(int groupPosition)
            {
                return _listDataHeader[groupPosition];
            }
            public override int GroupCount {
                get {
                    return _listDataHeader.Count;
                }
            }
            public override long GetGroupId(int groupPosition)
            {
                return groupPosition;
            }
            public override View GetGroupView(int groupPosition, bool isExpanded, View convertView, ViewGroup parent)
            {
                string headerTitle = (string)GetGroup(groupPosition);

                convertView = convertView ?? _context.LayoutInflater.Inflate(Resource.Layout.list_header, null);
                var lblListHeader = (TextView)convertView.FindViewById(Resource.Id.listHeader);
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

            class ViewHolderItem : Java.Lang.Object
            {
            }
        }
    }
}