using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.EncTool.Parser.Senadina;
using Hi3Helper.Http;
using System.Buffers;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hi3Helper.EncTool.Test
{
    internal class Program
    {
        private static ICryptoTransform CreateAESDecryptor(string identifier, string relativePath, int timestamp)
        {
            Aes aesInstance = Aes.Create();
            aesInstance.Mode = CipherMode.CFB;
            aesInstance.Key = SenadinaFileIdentifier.GenerateMothKey(identifier + relativePath);
            aesInstance.IV = SenadinaFileIdentifier.GenerateMothIV(timestamp);
            aesInstance.Padding = PaddingMode.ISO10126;

            return aesInstance.CreateDecryptor();
        }

        private static Stream CreateDecryptorStream(Stream sourceStream, string identifier, string relativePath, int timestamp)
        {
            ICryptoTransform transform = CreateAESDecryptor(identifier, relativePath, timestamp);
            CryptoStream decrypter = new CryptoStream(sourceStream, transform, CryptoStreamMode.Read);
            BrotliStream decoder = new BrotliStream(decrypter, CompressionMode.Decompress, true);
            return decoder;
        }

        static async Task Main(string[] args)
        {
            string? regionName = "Hi3CN";
            int[] regionVersion = new int[3] { 7, 3, 0 };
            string? cdnUrl = "https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/raw/main";
            string? referenceBaseUrl = ConverterTool.CombineURLFromString(cdnUrl, "metadata/game_reference_assets", regionName);
            string? identifierUrl = ConverterTool.CombineURLFromString(referenceBaseUrl, "identifier.json");

            using HttpClient httpClient = new HttpClient();
            using Stream identifierStream = await HttpResponseInputStream.CreateStreamAsync(httpClient, identifierUrl, null, null, default);

            Dictionary<string, SenadinaFileIdentifier>? identifier = await JsonSerializer.DeserializeAsync<Dictionary<string, SenadinaFileIdentifier>>(identifierStream, SenadinaJSONContext.Default.Options);

            string xmfBaseFileName = $"xmf/Blocks_{regionVersion[0]}_{regionVersion[1]}_base.xmf";
            string xmfBaseFileUrl = ConverterTool.CombineURLFromString(referenceBaseUrl, xmfBaseFileName);
            SenadinaFileIdentifier? xmfBaseIdentifier = identifier?[xmfBaseFileName];
            string? xmfBaseIdentifierString = string.Join('.', regionVersion);

            using HttpResponseInputStream xmfBaseInputNetworkStream = await HttpResponseInputStream.CreateStreamAsync(httpClient, xmfBaseFileUrl, null, null, default);
            using Stream xmfBaseInputDecoderStream = CreateDecryptorStream(xmfBaseInputNetworkStream, xmfBaseIdentifierString, xmfBaseFileName, (int?)xmfBaseIdentifier?.fileTime ?? 0);
            using FileStream xmfBaseOutputStream = File.Create("C:\\Users\\neon-nyan\\AppData\\LocalLow\\CollapseLauncher\\GameFolder\\_metadata\\test.xmf");

            byte[] buffer = ArrayPool<byte>.Shared.Rent(4 << 10);
            int read = 0;
            while ((read = await xmfBaseInputDecoderStream.ReadAsync(buffer)) > 0)
                xmfBaseOutputStream.Write(buffer, 0, read);
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