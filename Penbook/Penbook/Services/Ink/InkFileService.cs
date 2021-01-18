using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;

using Microsoft.Graphics.Canvas;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace Penbook.Services.Ink
{
    public class InkFileService
    {
        private readonly InkCanvas _inkCanvas;
        private readonly InkStrokesService _strokesService;
        private readonly Image _image;
        private readonly Grid _grid;

        public bool IsImageNotNull()
        {
            return _image.Source == null;
        }

        public InkFileService(Grid container, InkCanvas inkCanvas, Image image, InkStrokesService strokesService)
        {
            _inkCanvas = inkCanvas;
            _strokesService = strokesService;
            _image = image;
            _grid = container;
        }

        public async Task<bool> LoadInkAsync()
        {
            var openPicker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary
            };

            openPicker.FileTypeFilter.Add(".gif");

            var file = await openPicker.PickSingleFileAsync();
            return await _strokesService.LoadInkFileAsync(file);
        }

        public async Task<bool> LoadImageAsync()
        {
            var openPicker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary
            };

            openPicker.FileTypeFilter.Add(".png");
            openPicker.FileTypeFilter.Add(".jpg");

            var file = await openPicker.PickSingleFileAsync();

            if (file == null) return false;

            IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);

            var image = new BitmapImage();
                
            await image.SetSourceAsync(stream);
            _image.Source = image;

            stream.Dispose();

            return true;
        }

        public async Task<bool> LoadPdfAsync()
        {
            var openPicker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary
            };

            openPicker.FileTypeFilter.Add(".pdf");

            var file = await openPicker.PickSingleFileAsync();

            if (file == null) return false;

            var pdfDocument = await PdfDocument.LoadFromFileAsync(file);

            var page = pdfDocument.GetPage(0);

            var image = new BitmapImage();

            using (var stream = new InMemoryRandomAccessStream())
            {
                await page.RenderToStreamAsync(stream);
                await image.SetSourceAsync(stream);
                _image.Source = image;
            }

            return true;
        }

        public async Task SaveInkAsync()
        {
            if (!_strokesService.GetStrokes().Any())
            {
                return;
            }


            if (_image.Source != null)
            {
                var renderBitmap = new RenderTargetBitmap();
                await renderBitmap.RenderAsync(_grid);

                IBuffer pixelBuffer = await renderBitmap.GetPixelsAsync();
                byte[] pixels = pixelBuffer.ToArray();


                var stream = new InMemoryRandomAccessStream();
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, stream);

                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, (uint)renderBitmap.PixelWidth, (uint)renderBitmap.PixelHeight, 96, 96,
            pixels);

                await encoder.FlushAsync();
                stream.Seek(0);

                CanvasDevice device = CanvasDevice.GetSharedDevice();
                CanvasRenderTarget renderTarget = new CanvasRenderTarget(device, (int)_inkCanvas.ActualWidth, (int)_inkCanvas.ActualHeight, 96);

                using (var ds = renderTarget.CreateDrawingSession())
                {
                    ds.DrawInk(_inkCanvas.InkPresenter.StrokeContainer.GetStrokes());
                }


                _image.Source = renderBitmap;

                var savePicker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.PicturesLibrary
                };

                savePicker.FileTypeChoices.Add("", new List<string> { ".png" });

                var file = await savePicker.PickSaveFileAsync();

                using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await renderTarget.SaveAsync(fileStream, CanvasBitmapFileFormat.Png, 1f);
                }

                if (file == null) return;
            }

            else
            {
                var savePicker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.PicturesLibrary
                };
                var file = await savePicker.PickSaveFileAsync();
                await _strokesService.SaveInkFileAsync(file);
            }
            //BitmapImage inkimage = (BitmapImage)_image.Source;
            //    StorageFile inputFile = await StorageFile.GetFileFromApplicationUriAsync(inkimage.UriSource);
            //    CanvasDevice device = CanvasDevice.GetSharedDevice();
            //    CanvasRenderTarget renderTarget = new CanvasRenderTarget(device, (int)_inkCanvas.ActualWidth, (int)_inkCanvas.ActualHeight, 96);
            //    using (var ds = renderTarget.CreateDrawingSession())
            //    {
            //        var image = await CanvasBitmap.LoadAsync(device, inputFile.Path, 96);
            //        ds.DrawImage(image);// Draw image firstly
            //        ds.DrawInk(_inkCanvas.InkPresenter.StrokeContainer.GetStrokes());// Draw the stokes
            //    }

            //    //Save them to the output.jpg in picture folder
            //    StorageFolder storageFolder = KnownFolders.SavedPictures;
            //    //var file = await storageFolder.CreateFileAsync("output.jpg", CreationCollisionOption.ReplaceExisting);
            //    using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
            //    {
            //        await renderTarget.SaveAsync(fileStream, CanvasBitmapFileFormat.Jpeg, 1f);
            //    }
            //}
        }


        public async Task<StorageFile> ExportToImageAsync(StorageFile imageFile = null)
        {
            if (!_strokesService.GetStrokes().Any())
            {
                return null;
            }

            if (imageFile != null)
            {
                return await ExportCanvasAndImageAsync(imageFile);
            }
            else
            {
                return await ExportCanvasAsync();
            }
        }

        private async Task<StorageFile> ExportCanvasAndImageAsync(StorageFile imageFile)
        {
            var saveFile = await GetImageToSaveAsync();

            if (saveFile == null)
            {
                return null;
            }

            // Prevent updates to the file until updates are finalized with call to CompleteUpdatesAsync.
            CachedFileManager.DeferUpdates(saveFile);

            using (var outStream = await saveFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                var device = CanvasDevice.GetSharedDevice();

                CanvasBitmap canvasbitmap;
                using (var stream = await imageFile.OpenAsync(FileAccessMode.Read))
                {
                    canvasbitmap = await CanvasBitmap.LoadAsync(device, stream);
                }

                using (var renderTarget = new CanvasRenderTarget(device, (int)_inkCanvas.Width, (int)_inkCanvas.Height, canvasbitmap.Dpi))
                {
                    using (CanvasDrawingSession ds = renderTarget.CreateDrawingSession())
                    {
                        ds.DrawImage(canvasbitmap, new Rect(0, 0, (int)_inkCanvas.Width, (int)_inkCanvas.Height));
                        ds.DrawInk(_strokesService.GetStrokes());
                    }

                    await renderTarget.SaveAsync(outStream, CanvasBitmapFileFormat.Png);
                }
            }

            // Finalize write so other apps can update file.
            await CachedFileManager.CompleteUpdatesAsync(saveFile);

            return saveFile;
        }

        private async Task<StorageFile> ExportCanvasAsync()
        {
            var file = await GetImageToSaveAsync();

            if (file == null)
            {
                return null;
            }

            CanvasDevice device = CanvasDevice.GetSharedDevice();
            CanvasRenderTarget renderTarget = new CanvasRenderTarget(device, (int)_inkCanvas.Width, (int)_inkCanvas.Height, 96);

            using (var ds = renderTarget.CreateDrawingSession())
            {
                ds.DrawInk(_strokesService.GetStrokes());
            }

            using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                await renderTarget.SaveAsync(fileStream, CanvasBitmapFileFormat.Png);
            }

            return file;
        }

        private async Task<StorageFile> GetImageToSaveAsync()
        {
            var savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            savePicker.FileTypeChoices.Add("PNG", new List<string>() { ".png" });
            var saveFile = await savePicker.PickSaveFileAsync();

            return saveFile;
        }
    }
}
