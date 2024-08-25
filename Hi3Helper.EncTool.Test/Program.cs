using Hi3Helper.EncTool.Parser.AssetMetadata;
using System.IO.Compression;

namespace Hi3Helper.EncTool.Test
{
    internal class Program
    {


        static async Task Main(string[] args)
        {
            return; //frick testing
            #region unused

            using (SRMetadata srm = new SRMetadata("https://globaldp-prod-os01.starrails.com/query_dispatch", "3a57430d8d",
                "?version={0}{1}&t={2}&language_type=3&platform_type=3&channel_id=1&sub_channel_id=1&is_new_format=1",
                "?version={0}{1}&t={2}&uid=0&language_type=3&platform_type=3&dispatch_seed={3}&channel_id=1&sub_channel_id=1&is_need_url=1",
                "OSPRODWin", "1.5.0"))
            {
                await srm.Initialize(default, "prod_official_asia", "F:\\CollapseData\\SRGlb\\Games\\StarRail_Data\\Persistent");
                await srm.ReadAsbMetadataInformation(default);
                await srm.ReadBlockMetadataInformation(default);
                await srm.ReadAudioMetadataInformation(default);
                await srm.ReadVideoMetadataInformation(default);
                await srm.ReadIFixMetadataInformation(default);
                await srm.ReadDesignMetadataInformation(default);
                await srm.ReadLuaMetadataInformation(default);
            }

            return;

            string pata = @"C:\myGit\CollapseLauncher-ReleaseRepo\metadata\repair_indexes\Hi3Global\6.5.0\index.bin";
            using (FileStream fs = new FileStream(pata, FileMode.Open))
            {
                fs.Position = 9;
                using (BrotliStream br = new BrotliStream(fs, CompressionMode.Decompress))
                using (FileStream fsa = new FileStream(pata + ".dec", FileMode.Create))
                {
                    br.CopyTo(fsa);
                }
            }

            string path = @"C:\Program Files\Honkai Impact 3 sea\Games\BH3_Data\StreamingAssets\Asb\pc\Blocks.xmf";
            string pathTarget = @"G:\Data\CollapseLauncher\Hi3SEA\pc\";

            string URL = "https://d2wztyirwsuyyo.cloudfront.net/ptpublic/bh3_global/20230109172630_NRBvwa9qTB4EUXIz/extract/BH3_Data/StreamingAssets/Asb/pc/0fae6f42832ea16a84ac496c38437a1d.wmv";
            path = @"C:\Program Files\Honkai Impact 3rd glb\Games\BH3_Data\StreamingAssets\Asb\pc\0fae6f42832ea16a84ac496c38437a1d.wmv";

            using (FileStream fs = new(path, FileMode.Open, FileAccess.Write))
            {
                using (ChunkStream cs = new(fs, 0, 457688, false))
                {
                    Http.Http client = new(false);
                    await client.Download(URL, cs, 0, 457688, default, true);
                }
            }
            /*

            XMFParser parser = new XMFParser(path);
            foreach (string blockName in parser.EnumerateBlockHashString())
            {
                XMFBlock block = parser.GetBlockByHashString(blockName);
                string targetBlock = Path.Combine(pathTarget, block.HashString);

                if (!Directory.Exists(targetBlock))
                {
                    Directory.CreateDirectory(targetBlock);
                }

                Console.WriteLine($"Block hash: {block.HashString} Asset count: {block.AssetCount}");
                foreach (string assetName in block.EnumerateAssetNames())
                {
                    string targetAsset = Path.Combine(targetBlock, assetName);

                    XMFAsset asset = block.GetAssetByName(assetName);
                    using (Stream fs = asset.GetAssetStreamOpenRead())
                    {
                        byte[] buf = new byte[16 << 10];
                        int read;
                        using (Stream ft = new FileStream(targetAsset, FileMode.Create, FileAccess.Write))
                        {
                            while ((read = fs.Read(buf, 0, buf.Length)) > 0)
                            {
                                ft.Write(buf, 0, read);
                            }
                        }
                    }
                }
            }
            */
            #endregion
        }
    }
}