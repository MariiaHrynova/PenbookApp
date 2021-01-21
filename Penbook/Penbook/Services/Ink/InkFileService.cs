using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;

using Microsoft.Graphics.Canvas;
using Spire.Pdf;
using Spire.Pdf.Graphics;
using Spire.Pdf.Grid;
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

        public bool IsImageNull()
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

            var pdfDocument = await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(file);

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

            var savePicker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary
            };
            var file = await savePicker.PickSaveFileAsync();
            await _strokesService.SaveInkFileAsync(file);
        }


        public async Task ExportToImageAsync()
        {
            if (!_strokesService.GetStrokes().Any())
            {
                return;
            }

            if(_image.Source != null)
            {
                await SaveInkWithImage();
            }
            else
                await ExportCanvasAsync();
            
        }

        private async Task SaveInkWithImage()
        {
            var file = await GetImageToSaveAsync();

            if (file == null)
            {
                return;
            }


            var renderBitmap = new RenderTargetBitmap();
            await renderBitmap.RenderAsync(_grid);

            IBuffer pixelBuffer = await renderBitmap.GetPixelsAsync();
            byte[] pixels = pixelBuffer.ToArray();


            var stream = await file.OpenReadAsync();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, stream);

            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, (uint)renderBitmap.PixelWidth, (uint)renderBitmap.PixelHeight, 96, 96, pixels);

            //await encoder.FlushAsync();
            //stream.Seek(0);

            CanvasDevice device = CanvasDevice.GetSharedDevice();
            CanvasRenderTarget renderTarget = new CanvasRenderTarget(device, (int)_inkCanvas.ActualWidth, (int)_inkCanvas.ActualHeight, 96);

            _image.Source = renderBitmap;

            using (var ds = renderTarget.CreateDrawingSession())
            {
                ds.DrawInk(_strokesService.GetStrokes());
            }

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
            savePicker.FileTypeChoices.Add("JPG", new List<string>() { ".jpg" });
            var saveFile = await savePicker.PickSaveFileAsync();

            return saveFile;
        }

        private async Task<StorageFile> GetPdfToSaveAsync()
        {
            var savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            savePicker.FileTypeChoices.Add("PDF", new List<string>() { ".pdf" });
            var saveFile = await savePicker.PickSaveFileAsync();

            return saveFile;
        }

        public async Task ExportToPdfAsync()
        {
            if (!_strokesService.GetStrokes().Any())
            {
                return;
            }

            await SavePDF();
        }

        private async Task<StorageFile> SavePDF()
        {
            var file = await GetPdfToSaveAsync();

            if (file == null)
            {
                return null;
            }

            Spire.Pdf.PdfDocument doc = new Spire.Pdf.PdfDocument();

            PdfPageBase page = doc.Pages.Add();

            PdfGrid grid = new PdfGrid();

            PdfGridCellContentList lst = new PdfGridCellContentList();
            PdfGridCellContent textAndStyle = new PdfGridCellContent();

            var stream = await file.OpenReadAsync();
            System.IO.Stream inputStream = stream.AsStream();
            textAndStyle.Image = PdfImage.FromStream(inputStream);

            lst.List.Add(textAndStyle);

            doc.SaveToFile(file.Name, FileFormat.PDF);

            return file;
        }

    }
}
