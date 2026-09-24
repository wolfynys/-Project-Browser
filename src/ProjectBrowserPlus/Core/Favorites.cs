using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using Autodesk.Revit.DB;

namespace ProjectBrowserPlus.Core
{
    [DataContract]
    public class ProjectUserData
    {
        [DataMember] public string Title { get; set; }
        [DataMember] public List<string> Favorites { get; set; } = new List<string>();   // element UniqueIds
        [DataMember] public List<string> Recent { get; set; } = new List<string>();      // view UniqueIds, newest first
        [DataMember] public Dictionary<string, string> Notes { get; set; } = new Dictionary<string, string>(); // uniqueId -> note
        [DataMember] public Dictionary<string, string> Colors { get; set; } = new Dictionary<string, string>(); // uniqueId -> tag color
    }

    /// <summary>
    /// Per-project, per-user data (favorites, recent views, notes) stored as JSON in %AppData%.
    /// Nothing is written into the model, so worksharing / central files are never touched.
    /// </summary>
    public sealed class Favorites
    {
        private static readonly Dictionary<string, Favorites> Cache = new Dictionary<string, Favorites>();
        private readonly string _path;
        private readonly HashSet<string> _fav;
        public ProjectUserData Data { get; }

        private Favorites(string path, ProjectUserData data)
        {
            _path = path; Data = data;
            _fav = new HashSet<string>(data.Favorites);
        }

        public static Favorites For(Document doc)
        {
            var key = KeyFor(doc);
            if (Cache.TryGetValue(key, out var f)) return f;
            var path = Path.Combine(Log.Dir, "projects", key + ".json");
            var data = Json.Load<ProjectUserData>(path);
            data.Title = doc.Title;
            f = new Favorites(path, data);
            Cache[key] = f;
            return f;
        }

        public static string KeyFor(Document doc)
        {
            string id = null;
            try
            {
                if (doc.IsWorkshared)
                {
                    var mp = doc.GetWorksharingCentralModelPath();
                    if (mp != null) id = ModelPathUtils.ConvertModelPathToUserVisiblePath(mp);
                }
            }
            catch { }
            if (string.IsNullOrEmpty(id)) id = string.IsNullOrEmpty(doc.PathName) ? "untitled:" + doc.Title : doc.PathName;
            using (var sha = SHA1.Create())
            {
                var h = sha.ComputeHash(Encoding.UTF8.GetBytes(id.ToLowerInvariant()));
                return BitConverter.ToString(h, 0, 8).Replace("-", "").ToLowerInvariant();
            }
        }

        public bool IsFavorite(string uid) => uid != null && _fav.Contains(uid);

        public bool Toggle(string uid)
        {
            if (uid == null) return false;
            bool now;
            if (_fav.Contains(uid)) { _fav.Remove(uid); now = false; }
            else { _fav.Add(uid); now = true; }
            Data.Favorites = _fav.ToList();
            Save();
            return now;
        }

        public void Set(string uid, bool value)
        {
            if (uid == null) return;
            if (value) _fav.Add(uid); else _fav.Remove(uid);
            Data.Favorites = _fav.ToList();
            Save();
        }

        public void PushRecent(string uid)
        {
            if (uid == null) return;
            Data.Recent.Remove(uid);
            Data.Recent.Insert(0, uid);
            var max = Math.Max(3, Settings.Current.MaxRecent);
            if (Data.Recent.Count > max) Data.Recent.RemoveRange(max, Data.Recent.Count - max);
            Save();
        }

        public string GetNote(string uid) => uid != null && Data.Notes.TryGetValue(uid, out var n) ? n : null;
        public void SetNote(string uid, string note)
        {
            if (uid == null) return;
            if (string.IsNullOrWhiteSpace(note)) Data.Notes.Remove(uid); else Data.Notes[uid] = note.Trim();
            Save();
        }

        public string GetColor(string uid) => uid != null && Data.Colors.TryGetValue(uid, out var c) ? c : null;
        public void SetColor(string uid, string color)
        {
            if (uid == null) return;
            if (string.IsNullOrEmpty(color)) Data.Colors.Remove(uid); else Data.Colors[uid] = color;
            Save();
        }

        public void Save() => Json.Save(_path, Data);
    }
}
