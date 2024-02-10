// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Markdig.Syntax.Inlines;
using HtmlAgilityPack;
using System.Globalization;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Net.Http;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.TextElements;

internal class MyImage : IAddChild
{
    private InlineUIContainer _container = new InlineUIContainer();
    private LinkInline? _linkInline;
    private Image _image = new Image();
    private Grid _imageGrid;
    private Uri _uri;
    private HtmlNode? _htmlNode;
    private IImageProvider? _imageProvider;
    private ISVGRenderer _svgRenderer;
    private double _precedentWidth;
    private double _precedentHeight;
    private bool _loaded;

    public TextElement TextElement
    {
        get => _container;
    }

    public MyImage(LinkInline linkInline, Uri uri, MarkdownConfig config)
    {
        _linkInline = linkInline;
        _uri = uri;
        _imageProvider = config.ImageProvider;
        _svgRenderer = config.SVGRenderer == null ? new DefaultSVGRenderer() : config.SVGRenderer;
        Init();
        var size = Extensions.GetMarkdownImageSize(linkInline);
        if (size.Width != 0)
        {
            _precedentWidth = size.Width;
        }
        if (size.Height != 0)
        {
            _precedentHeight = size.Height;
        }
    }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public MyImage(HtmlNode htmlNode, MarkdownConfig? config)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    {
#pragma warning disable CS8601 // Possible null reference assignment.
        Uri.TryCreate(htmlNode.GetAttributeValue("src", "#"), UriKind.RelativeOrAbsolute, out _uri);
#pragma warning restore CS8601 // Possible null reference assignment.
        _htmlNode = htmlNode;
        _imageProvider = config?.ImageProvider;
        _svgRenderer = config?.SVGRenderer == null ? new DefaultSVGRenderer() : config.SVGRenderer;
        Init();
        int.TryParse(
            htmlNode.GetAttributeValue("width", "0"),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var width
        );
        int.TryParse(
            htmlNode.GetAttributeValue("height", "0"),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var height
        );
        if (width > 0)
        {
            _precedentWidth = width;
        }
        if (height > 0)
        {
            _precedentHeight = height;
        }
    }

    private void Init()
    {
        _image.Loaded += LoadImage;
        _imageGrid = new Grid { CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 0, 8) };
        _imageGrid.Children.Add(_image);
        _container.Child = _imageGrid;
    }

    private async void LoadImage(object sender, RoutedEventArgs e)
    {
        if (_loaded) return;
        try
        {
            if (_imageProvider != null && _imageProvider.ShouldUseThisProvider(_uri.AbsoluteUri))
            {
                _image = await _imageProvider.GetImage(_uri.AbsoluteUri);
                _container.Child = _image;
            }
            else
            {
                HttpClient client = new HttpClient();

                // Download data from URL
                HttpResponseMessage response = await client.GetAsync(_uri);


                // Get the Content-Type header
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                string contentType = response.Content.Headers.ContentType.MediaType;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.

                if (contentType == "image/svg+xml")
                {
                    var svgString = await response.Content.ReadAsStringAsync();
                    var resImage = await _svgRenderer.SvgToImage(svgString);
                    if (resImage != null)
                    {
                        _image = resImage;
                        _container.Child = _image;
                    }
                }
                else
                {
                    byte[] data = await response.Content.ReadAsByteArrayAsync();
                    // Create a BitmapImage for other supported formats
                    BitmapImage bitmap = new BitmapImage();
                    using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
                    {
                        // Write the data to the stream
                        await stream.WriteAsync(data.AsBuffer());
                        stream.Seek(0);

                        // Set the source of the BitmapImage
                        await bitmap.SetSourceAsync(stream);
                    }
                    _image.Source = bitmap;
                    // _image.Width = bitmap.PixelWidth == 0 ? bitmap.DecodePixelWidth : bitmap.PixelWidth;
                    // _image.Height = bitmap.PixelHeight == 0 ? bitmap.DecodePixelHeight : bitmap.PixelHeight;

                }

                _loaded = true;
            }

            if (_precedentWidth != 0)
            {
                _image.Width = _precedentWidth;
            }
            if (_precedentHeight != 0)
            {
                _image.Height = _precedentHeight;
            }
        }
        catch (Exception) { }
    }

    public void AddChild(IAddChild child) {}
}
