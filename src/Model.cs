using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Linq;
using System.Security.Cryptography;

namespace PaperNest {
    public static class PaperTilt {
        static readonly Random random=new Random();
        static readonly double[] naturalAngles={-3.5,-3,-2.5,-2,-1.5,-1,1,1.5,2,2.5,3,3.5};
        public static double Normalize(double angle){return Double.IsNaN(angle)||Double.IsInfinity(angle)?0:Math.Round(Math.Max(-5,Math.Min(5,angle)),1);}
        public static double RandomAngle(double previous){previous=Normalize(previous);List<double> choices=new List<double>();foreach(double angle in naturalAngles)if(Math.Abs(angle-previous)>=.75)choices.Add(angle);lock(random)return choices[random.Next(choices.Count)];}
        public static string Description(double angle){return Math.Abs(angle)<.05?L.T("放正 · 0°"):(angle<0?L.T("左倾 "):L.T("右倾 "))+Math.Abs(angle).ToString("0.#",System.Globalization.CultureInfo.InvariantCulture)+"°";}
    }
    [DataContract] public sealed class NoteData {
        [DataMember] public string Id = Guid.NewGuid().ToString("N");
        [DataMember] public string Title = L.T("新的小纸条");
        [DataMember] public string Kind = "text";
        [DataMember] public string Color = "butter";
        [DataMember] public string Decoration = "tape";
        [DataMember] public double Left = 100;
        [DataMember] public double Top = 100;
        [DataMember] public double Width = 410;
        [DataMember] public double Height = 460;
        [DataMember] public double Zoom = 1;
        [DataMember] public double Angle = -1.5;
        [DataMember] public bool Pinned;
        [DataMember] public bool Locked;
        [DataMember] public bool Hidden;
        [DataMember] public bool Deleted;
        [DataMember] public string Text = "";
        [DataMember] public string Checks = "";
        [DataMember] public List<string> Items;
        [DataMember] public string ImageFile = "";
        [DataMember] public string DocumentFile = "";
        [DataMember] public string DocumentRevision = "";
        [DataMember] public string RecoverySourceKey = "";
        [DataMember] public DateTime Updated = DateTime.UtcNow;
        [DataMember] public DateTime ContentUpdated=DateTime.SpecifyKind(DateTime.MinValue,DateTimeKind.Utc);
        [DataMember] public DateTime DocumentSavedAt=DateTime.SpecifyKind(DateTime.MinValue,DateTimeKind.Utc);
    }
    [DataContract] public sealed class AppData {
        [DataMember] public int FormatVersion = 1;
        [DataMember] public string Language = L.Code;
        [DataMember] public bool StartWithWindows;
        [DataMember] public bool ShowOnLaunch = true;
        [DataMember] public bool Texture = true;
        [DataMember] public double DefaultZoom = 1;
        [DataMember] public string DefaultColor = "butter";
        [DataMember] public List<NoteData> Notes = new List<NoteData>();
    }
    public sealed class Store {
        public readonly string Root;
        public AppData Data;
        public string LoadWarning = "";
        public DateTime LastSavedAt;
        DateTime lastHistoryAt=DateTime.MinValue;
        string stateHash="";
        public string StatePath { get { return Path.Combine(Root, "notes.json"); } }
        public string DraftRoot { get { return Path.Combine(Root,"Drafts"); } }
        public string HistoryRoot { get { return Path.Combine(Root,"History"); } }
        public Store(string root) {
            Root = Path.GetFullPath(root);
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(Path.Combine(Root, "Documents"));
            Directory.CreateDirectory(Path.Combine(Root, "Images"));
            Directory.CreateDirectory(DraftRoot);Directory.CreateDirectory(HistoryRoot);
            Data = Load();
            stateHash=File.Exists(StatePath)?Hash(ReadSnapshot(StatePath)):"";
        }
        // Atomic replacement must remain possible while another instance reads a snapshot.
        internal static FileStream OpenSnapshot(string path){return new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete);}
        internal static DateTime NextUpdate(DateTime previous){
            // DataContract JSON stores milliseconds, so sub-millisecond edits need distinct stamps.
            long now=DateTime.UtcNow.Ticks/TimeSpan.TicksPerMillisecond;
            long last=previous.ToUniversalTime().Ticks/TimeSpan.TicksPerMillisecond;
            return new DateTime(Math.Max(now,last+1)*TimeSpan.TicksPerMillisecond,DateTimeKind.Utc);
        }
        static byte[] ReadSnapshot(string path){using(FileStream stream=OpenSnapshot(path))using(MemoryStream bytes=new MemoryStream()){stream.CopyTo(bytes);return bytes.ToArray();}}
        AppData Read(string path) {
            using (FileStream stream = OpenSnapshot(path)) {
                AppData data = (AppData)new DataContractJsonSerializer(typeof(AppData)).ReadObject(stream);
                if (data == null || data.Notes == null || data.FormatVersion != 1) throw new InvalidDataException("Unsupported notes file");
                foreach(NoteData note in data.Notes)NormalizeDates(note);
                return data;
            }
        }
        AppData Load() {
            AppData loaded=null;bool hadFiles=File.Exists(StatePath)||File.Exists(StatePath+".bak")||HistoryFiles().Any()||Directory.GetFiles(DraftRoot,"*.json*").Any()||Directory.GetFiles(Path.Combine(Root,"Documents"),"*.xamlpackage").Any();
            if(File.Exists(StatePath))try{loaded=Read(StatePath);}catch(Exception ex){Log(ex);}
            if(loaded==null&&File.Exists(StatePath+".bak"))try{loaded=Read(StatePath+".bak");LoadWarning=L.T("已从上一份备份恢复便签。");}catch(Exception ex){Log(ex);}
            if(loaded==null||loaded.Notes.Count==0) {
                foreach(string file in HistoryFiles())try{AppData candidate=Read(file);if(candidate.Notes.Count>0){loaded=candidate;LoadWarning=L.T("已从历史快照恢复便签。");break;}}catch{}
            }
            if(loaded==null)loaded=new AppData();
            int recovered=0;
            foreach(string file in Directory.GetFiles(DraftRoot,"*.json")) {
                NoteData draft=ReadDraft(file);if(draft==null)draft=ReadDraft(file+".bak");if(draft==null)continue;
                NoteData current=loaded.Notes.FirstOrDefault(n=>n.Id==draft.Id);
                if(current==null){loaded.Notes.Add(draft);recovered++;}
                else if(draft.Updated>current.Updated||(draft.Updated==current.Updated&&!Serialize(draft,typeof(NoteData)).SequenceEqual(Serialize(current,typeof(NoteData))))){loaded.Notes[loaded.Notes.IndexOf(current)]=draft;recovered++;}
            }
            // A missing manifest must not orphan the corresponding readable backup drafts.
            foreach(string file in Directory.GetFiles(DraftRoot,"*.json.bak")) {
                if(File.Exists(file.Substring(0,file.Length-4)))continue;NoteData draft=ReadDraft(file);if(draft!=null&&!loaded.Notes.Any(n=>n.Id==draft.Id)){loaded.Notes.Add(draft);recovered++;}
            }
            if(recovered>0)LoadWarning=L.T("已恢复 ")+recovered+L.T(" 张未完成主清单写入的草稿。");
            if(hadFiles&&loaded.Notes.Count==0)throw new InvalidDataException(L.T("主文件与备份未能恢复；已保留原文件，请使用数据恢复副本。")+Root);
            return loaded;
        }
        public void Save() {
            if(File.Exists(StatePath)) {
                byte[] diskBytes=ReadSnapshot(StatePath);
                if(Hash(diskBytes)!=stateHash)try{
                    AppData disk=Read(StatePath);WriteHistory(diskBytes,true);
                    foreach(NoteData note in disk.Notes)if(!Data.Notes.Any(n=>n.Id==note.Id))Data.Notes.Add(note);
                }catch(Exception ex){Log(ex);}
            }
            byte[] bytes=Serialize(Data,typeof(AppData));
            AtomicWrite(StatePath, bytes, true);
            stateHash=Hash(bytes);LastSavedAt=DateTime.Now;
            WriteHistory(bytes,false);
        }
        public int ImportLegacyWorkspace(string sourceRoot) {
            sourceRoot=Path.GetFullPath(sourceRoot).TrimEnd(Path.DirectorySeparatorChar);
            if(String.Equals(sourceRoot,Root.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase))return 0;
            string marker=Path.Combine(Root,".imported-"+Hash(Encoding.UTF8.GetBytes(sourceRoot.ToLowerInvariant())).Replace("-","").Substring(0,20));
            if(File.Exists(marker))return 0;
            if(!File.Exists(Path.Combine(sourceRoot,"notes.json")))throw new FileNotFoundException(L.T("原便签数据不存在，尚未执行恢复。"),sourceRoot);
            Store source=new Store(sourceRoot);int imported=0;
            foreach(NoteData sourceNote in source.Data.Notes) {
                NoteData existing=Data.Notes.FirstOrDefault(n=>n.Id==sourceNote.Id);if(existing!=null&&existing.Updated>=sourceNote.Updated)continue;
                NoteData note=Clone(sourceNote);NormalizeDates(note);
                string document=source.DocumentPath(note),destination=DocumentPath(note);
                if(File.Exists(document))AtomicWrite(destination,File.ReadAllBytes(document),true);
                string revision=source.VersionDocumentPath(note);if(File.Exists(revision))SaveDocumentVersion(note,File.ReadAllBytes(revision));
                string image=source.ImagePath(note.ImageFile);if(File.Exists(image)){string target=ImagePath(note.ImageFile);if(!File.Exists(target))File.Copy(image,target);}
                if(existing==null)Data.Notes.Add(note);else Data.Notes[Data.Notes.IndexOf(existing)]=note;
                SaveDraft(note);imported++;
            }
            Save();AtomicWrite(marker,Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("o")),false);
            return imported;
        }
        static string Hash(byte[] bytes){using(SHA256 hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes));}
        static byte[] Serialize(object value,Type type){using(MemoryStream memory=new MemoryStream()){new DataContractJsonSerializer(type).WriteObject(memory,value);return memory.ToArray();}}
        public void SaveDraft(NoteData note){Guid guid;if(!Guid.TryParseExact(note.Id,"N",out guid))throw new InvalidDataException("Invalid draft id");AtomicWrite(Path.Combine(DraftRoot,note.Id+".json"),Serialize(note,typeof(NoteData)),true);}
        static void NormalizeDates(NoteData note){if(note.ContentUpdated==DateTime.MinValue)note.ContentUpdated=DateTime.SpecifyKind(DateTime.MinValue,DateTimeKind.Utc);if(note.DocumentSavedAt==DateTime.MinValue)note.DocumentSavedAt=DateTime.SpecifyKind(DateTime.MinValue,DateTimeKind.Utc);}
        NoteData ReadDraft(string path){try{using(FileStream stream=OpenSnapshot(path)){NoteData n=(NoteData)new DataContractJsonSerializer(typeof(NoteData)).ReadObject(stream);Guid g;if(n==null||!Guid.TryParseExact(n.Id,"N",out g))return null;NormalizeDates(n);return n;}}catch{return null;}}
        public static NoteData Clone(NoteData note){byte[] bytes=Serialize(note,typeof(NoteData));using(MemoryStream memory=new MemoryStream(bytes))return (NoteData)new DataContractJsonSerializer(typeof(NoteData)).ReadObject(memory);}
        public IEnumerable<string> HistoryFiles(){return Directory.GetFiles(HistoryRoot,"*.json").OrderByDescending(File.GetLastWriteTimeUtc);}
        public AppData ReadHistory(string path){string full=Path.GetFullPath(path);if(!full.StartsWith(HistoryRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Invalid history path");return Read(full);}
        public void SnapshotNow(){WriteHistory(Serialize(Data,typeof(AppData)),true);}
        void WriteHistory(byte[] bytes,bool force){
            try {
                DateTime now=DateTime.Now;string daily=Path.Combine(HistoryRoot,"daily-"+now.ToString("yyyyMMdd")+".json");
                if(!File.Exists(daily))AtomicWrite(daily,bytes,false);
                if(!force&&(now-lastHistoryAt).TotalSeconds<30)return;
                string file=Path.Combine(HistoryRoot,now.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".json");AtomicWrite(file,bytes,false);lastHistoryAt=now;
                foreach(string old in Directory.GetFiles(HistoryRoot,"*.json").Where(f=>!Path.GetFileName(f).StartsWith("daily-")).OrderByDescending(File.GetLastWriteTimeUtc).Skip(200))File.Delete(old);
                foreach(string old in Directory.GetFiles(HistoryRoot,"daily-*.json").OrderByDescending(File.GetLastWriteTimeUtc).Skip(30))File.Delete(old);
            }catch(Exception ex){Log(ex);}
        }
        public static void AtomicWrite(string path, byte[] bytes, bool backup) {
            string tmp = path + ".new";
            using (FileStream stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None)) {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(tmp, path, backup ? path + ".bak" : null);
            else File.Move(tmp, path);
        }
        public string DocumentPath(NoteData note) {
            // Only locally generated identifiers may become filesystem paths.
            Guid parsed;
            if (!Guid.TryParseExact(note.Id, "N", out parsed)) throw new InvalidDataException("Invalid note id");
            return Path.Combine(Root, "Documents", parsed.ToString("N") + ".xamlpackage");
        }
        public string VersionDocumentPath(NoteData note) {
            string revision=note.DocumentRevision;
            if(note.ContentUpdated>note.DocumentSavedAt)return "";
            if(String.IsNullOrEmpty(revision)||revision.Length!=64||revision.Any(c=>!Uri.IsHexDigit(c)))return "";
            return Path.Combine(Root,"DocumentVersions",revision+".xamlpackage");
        }
        public void SaveDocumentVersion(NoteData note,byte[] bytes){
            string revision=Hash(bytes).Replace("-","");string folder=Path.Combine(Root,"DocumentVersions");Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,revision+".xamlpackage");if(!File.Exists(path))AtomicWrite(path,bytes,false);note.DocumentRevision=revision;
        }
        public string ImportImage(string path) {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".bmp" && ext != ".gif" && ext != ".tif" && ext != ".tiff") throw new InvalidDataException(L.T("请选择 PNG、JPG、BMP、GIF 或 TIFF 图片。"));
            string name = Guid.NewGuid().ToString("N") + ext;
            File.Copy(path, Path.Combine(Root, "Images", name));
            return name;
        }
        public string ImagePath(string name) {
            if (String.IsNullOrEmpty(name) || Path.GetFileName(name) != name) return "";
            return Path.Combine(Root, "Images", name);
        }
        public void Log(Exception ex) {
            try { File.AppendAllText(Path.Combine(Root, "errors.log"), DateTime.Now.ToString("s") + " " + ex + Environment.NewLine, Encoding.UTF8); } catch { }
        }
    }
}
