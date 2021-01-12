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

namespace Penbook.Services.Ink
{
    public partial class InkFileService
    {
        private PrintDocument printDocument;
        private PrintManager printManager;
        private PrintHelper printHelper;

        private List<UIElement> printPreviewPages;

        private IPrintDocumentSource printDocumentSource;

        public bool CanPrint { get; private set; }

        public async Task RegisterForPrinting()
        {
            printDocument = new PrintDocument();
            printDocumentSource = printDocument.DocumentSource;
           
            printManager = PrintManager.GetForCurrentView();
            printManager.PrintTaskRequested += PrintTaskRequested;

            CanPrint = true;
        }

       
        private void GetPrintPreviewPage(object sender, GetPreviewPageEventArgs args)
        {
            var printDoc = (PrintDocument)sender;
            printDoc.SetPreviewPage(args.PageNumber, printPreviewPages[args.PageNumber - 1]);
        }

        private void PrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs args)
        {
            PrintTask printTask = null;
            printTask = args.Request.CreatePrintTask("Print drawing", sourceRequested =>
            {
                // Print Task event handler is invoked when the print job is completed.
                printTask.Completed += async (s, e) =>
                {
                    // Notify the user when the print operation fails.
                    if (e.Completion == PrintTaskCompletion.Failed)
                    {
                        ContentDialog noPrintingDialog = new ContentDialog()
                        {
                            Title = "ОШИБКА ПЕЧАТИ",
                            Content = "\nПЕЧАТЬ НЕ УДАЛАСЬ.",
                            PrimaryButtonText = "OK"
                        };
                        await noPrintingDialog.ShowAsync();
                    }
                };

                sourceRequested.SetSource(printDocumentSource);
            });
        }

        private void PrintTaskSourceRequrested(PrintTaskSourceRequestedArgs args)
        {
            // Set the document source.
            args.SetSource(printDocumentSource);
        }


        private void AddPages(object sender, AddPagesEventArgs e) //ОШИБКА ПРИ ПОПЫТКЕ НАПЕЧАТАТЬ
        {
            var printDoc = (PrintDocument)sender;

            // Add each preview page to the print document.
            foreach (var previewPage in printPreviewPages)
            {
                printDoc.AddPage(previewPage);
            }

            // Indicate that all of the print pages have been provided
            printDoc.AddPagesComplete();
        }


        public void UnregisterForPrinting()
        {
            if (printDocument == null)
            {
                return;
            }
            printDocument.AddPages -= AddPages;
            // Remove the handler for printing initialization.
            PrintManager printMan = PrintManager.GetForCurrentView();
            printMan.PrintTaskRequested -= PrintTaskRequested;
        }


        public async Task PrintAsync()
        {
            if (PrintManager.IsSupported())
            {
                try
                {
                    await PrintManager.ShowPrintUIAsync();
                }
                catch
                {
                    // Printing cannot proceed at this time
                    ContentDialog noPrintingDialog = new ContentDialog()
                    {
                        Title = "Printing error",
                        Content = "\nSorry, printing can' t proceed at this time.",
                        PrimaryButtonText = "OK"
                    };
                    await noPrintingDialog.ShowAsync();
                }
            }
            else
            {
                // Printing is not supported on this device
                ContentDialog noPrintingDialog = new ContentDialog()
                {
                    Title = "Printing not supported",
                    Content = "\nSorry, printing is not supported on this device.",
                    PrimaryButtonText = "OK"
                };
                await noPrintingDialog.ShowAsync();
            }

        }
    }
}

