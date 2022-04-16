using System;
using AssetStudio;

namespace MyApp // Note: actual namespace depends on the project name.
{
    internal class Program
    {
        static Dictionary<long, NamedObject> PatchObjs = new Dictionary<long, NamedObject>();
        static Dictionary<long, NamedObject> OldObjs = new Dictionary<long, NamedObject>();

        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            var oldpath = "/Users/luxuia/Documents/Demo/Assets/tex";

            var assetsmanager = new AssetsManager();
            assetsmanager.LoadFiles(new string[] {oldpath});

            var newpath = "/Users/luxuia/Documents/Demo/Assets/tex_new";
            var patchassetsmgr = new AssetsManager();
            patchassetsmgr.LoadFiles(new string[] { newpath });

            foreach (var assetFile in patchassetsmgr.assetsFileList)
            {
                foreach (var obj in assetFile.Objects)
                {
             
                    PatchObjs.Add(obj.m_PathID, (NamedObject)obj);
                }
            }


            var topatchlist = new List<NamedObject>();
            foreach(var assetFile in assetsmanager.assetsFileList)
            {
                foreach ( var obj in assetFile.Objects)
                {
                    if (PatchObjs.ContainsKey(obj.m_PathID))
                    {
                        switch ( obj)
                        {
                            case Texture2D tex2d:
                                Console.WriteLine("tex2d, name {0} pathid: {1} ", tex2d.m_Name, tex2d.m_PathID);
                                topatchlist.Add(tex2d);
                                break;
                            case NamedObject nameobj:
                                Console.WriteLine("tex2d, name {0} pathid: {1} ", nameobj.m_Name, nameobj.m_PathID);
                                break;
                            default:
                                break;
                        }
                    }
                }
            }

            var ret = "/Users/luxuia/Documents/Demo/Assets/tex_patched";

            TestWriteBundleFile();

            foreach (var obj in topatchlist)
            {
                switch (obj)
                {
                    case Texture2D tex2d:
                        var filename = Path.GetFileName(tex2d.m_StreamData.path);
                        var resblock = assetsmanager.resourceFileReaders[filename];
                        //var offset = resblock.Position;
                        break;
                    default:
                        break;
                }
            }
        }


        static void TestWriteBundleFile()
        {
            var oldpath = "/Users/luxuia/Documents/Demo/Assets/tex";
            var ret = "/Users/luxuia/Documents/Demo/Assets/tex_patched";
            var reader = new FileReader(oldpath);

            var bundleFile = new BundleFile(reader);

            using (var streamer = File.Open(ret, FileMode.Create, FileAccess.Write))
            {
                bundleFile.Write(streamer);
            }


            var patchbundle = new BundleFile(new FileReader(ret));
            foreach (var file in patchbundle.fileList)
            {

            }
        }
    }
}