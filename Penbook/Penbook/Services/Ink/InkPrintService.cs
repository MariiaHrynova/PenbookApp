using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Printing;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Printing;
using Microsoft.Toolkit.Uwp.Helpers;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;
using System.Resources;

namespace Penbook.Services.Ink
{
    public class InkPrintService
    { 
        private readonly InkCanvas inkCanvas;
        private Panel container;

        ///private IPrintDocumentSource printDocumentSource;

        private PrintHelper printHelper;
        public bool CanPrint { get; private set; } = true;

        public InkPrintService(InkCanvas inkCanvas, Panel container)
        {
            this.container = container;
            this.inkCanvas = inkCanvas;
        }

        public void RegisterForPrinting()
        {
            //printDocument = new PrintDocument();
            //printDocumentSource = printDocument.DocumentSource;
            //printDocument.GetPreviewPage += GetPrintPreviewPage;

            printHelper = new PrintHelper(container);

        }


        private void PrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs args)
        {
            PrintTask printTask = null;
            printTask = args.Request.CreatePrintTask("Print drawing", sourceRequested =>
            {
                printTask.Completed += async (s, e) =>
                {

                    if (e.Completion == PrintTaskCompletion.Failed)
                    {
                        var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();

                        ContentDialog noPrintingDialog = new ContentDialog()
                        { 
                            Title = resourceLoader.GetString("noPrintingDialog.Title"),                   
                            Content = "noPrintingDialog.Content",
                            PrimaryButtonText = "noPrintingDialog.Ok"
                        };
                        await noPrintingDialog.ShowAsync();
                    }
                };

                //sourceRequested.SetSource(printDocumentSource);
            });
        }

        public void UnregisterForPrinting()
        {
            PrintManager printMan = PrintManager.GetForCurrentView();
            printMan.PrintTaskRequested -= PrintTaskRequested;
        }

        public async Task PrintAsync()
        {
            RegisterForPrinting();

            var inkStream = new InMemoryRandomAccessStream();
            await inkCanvas.InkPresenter.StrokeContainer.SaveAsync(inkStream.GetOutputStreamAt(0));
            var inkBitmap = new BitmapImage();
            await inkBitmap.SetSourceAsync(inkStream);

            var inkViewbox = new Viewbox()
            {
                Child = new Image()
                {
                    Source = inkBitmap
                },
                Width = inkCanvas.ActualWidth,
                Height = inkCanvas.ActualHeight
            };

            printHelper.AddFrameworkElementToPrint(inkViewbox);

            try
            {
                await printHelper.ShowPrintUIAsync("printing drawing");
            }
            catch
            {
                var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
                
                ContentDialog noPrintingDialog = new ContentDialog()
                {
                    Title = resourceLoader.GetString("noPrintingDialog.Title"),
                    Content = resourceLoader.GetString("noPrintingDialog.Content"),    
                    PrimaryButtonText = resourceLoader.GetString("noPrintingDialog.Ok")
                };
                await noPrintingDialog.ShowAsync();
            }

        }
    }
}


