#if !DISABLEDISCORD
using System;
#if UNITY_EDITOR || UNITY_STANDALONE
using UnityEngine;
#endif

namespace Discord
{
    public struct ImageHandle
    {
        static public ImageHandle User(Int64 id)
        {
            return User(id, 128);
        }

        static public ImageHandle User(Int64 id, UInt32 size)
        {
            return new ImageHandle
            {
                Type = ImageType.User,
                Id = id,
                Size = size,
            };
        }

        public ImageType Type;

        public Int64 Id;

        public UInt32 Size;
    }

    public partial class ImageManager
    {
        public void Fetch(ImageHandle handle, FetchHandler callback)
        {
            Fetch(handle, false, callback);
        }

        public byte[] GetData(ImageHandle handle)
        {
            var dimensions = GetDimensions(handle);
            var data = new byte[dimensions.Width * dimensions.Height * 4];
            GetData(handle, data);
            return data;
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        public Texture2D GetTexture(ImageHandle handle)
        {
            var dimensions = GetDimensions(handle);
            var texture = new Texture2D((int)dimensions.Width, (int)dimensions.Height, TextureFormat.RGBA32, false, true);
            texture.LoadRawTextureData(GetData(handle));
            texture.Apply();
            return texture;
        }
#endif
    }
}
#endif