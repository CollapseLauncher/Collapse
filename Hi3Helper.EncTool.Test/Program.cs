namespace Hi3Helper.EncTool.Test
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string path = @"C:\Program Files\Honkai Impact 3 sea\Games\BH3_Data\StreamingAssets\Asb\pc\Blocks.xmf";
            string pathTarget = @"G:\Data\CollapseLauncher\Hi3SEA\pc\";

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
        }
    }
}