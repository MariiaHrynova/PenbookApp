using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Printing;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Printing;

namespace Penbook.Services.Ink.UndoRedo
{
    public class InkPrintService
    {
        private PrintDocument printDocument;
        private PrintManager printManager;
        private IPrintDocumentSource printDocumentSource;
        public InkPrintService()
        {

        }

        public virtual void RegisterForPrinting()
        {
            //printDocument = new PrintDocument();
            //printDocumentSource = printDocument.DocumentSource;
            //printDocument.Paginate += CreatePrintPreviewPages;
            //printDocument.GetPreviewPage += GetPrintPreviewPage;
            //printDocument.AddPages += AddPrintPages;

            //PrintManager printMan = PrintManager.GetForCurrentView();
            //printMan.PrintTaskRequested += PrintTaskRequested;
        }

        //protected override void OnNavigatedTo(NavigationEventArgs e)
        //{
        //    // Initialize common helper class and register for printing
        //    printHelper = new PrintHelper(this);
        //    printHelper.RegisterForPrinting();

        //    // Initialize print content for this scenario
        //    printHelper.PreparePrintContent(new PageToPrint());

        //    // Tell the user how to print
        //    MainPage.Current.NotifyUser("Print contract registered with customization, use the Print button to print.", NotifyType.StatusMessage);
        //}
    }
}
