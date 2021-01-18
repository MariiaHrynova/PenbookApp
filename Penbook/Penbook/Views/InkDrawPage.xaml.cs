using System;
using Penbook.Services.Ink;
using Penbook.ViewModels;
using Windows.UI.Xaml.Controls;

namespace Penbook.Views
{
    // For more information regarding Windows Ink documentation and samples see https://github.com/Microsoft/WindowsTemplateStudio/blob/release/docs/UWP/pages/ink.md
    public sealed partial class InkDrawPage : Page
    {
        public InkDrawViewModel ViewModel { get; } = new InkDrawViewModel();

        public InkDrawPage()
        {
            InitializeComponent();

            Loaded += (sender, eventArgs) =>
            {
                SetCanvasSize();

                var strokeService = new InkStrokesService(inkCanvas.InkPresenter);
                var selectionRectangleService = new InkSelectionRectangleService(inkCanvas, selectionCanvas, strokeService);

                ViewModel.Initialize(
                    strokeService,
                    new InkLassoSelectionService(inkCanvas, selectionCanvas, strokeService, selectionRectangleService),
                    new InkPointerDeviceService(inkCanvas),
                    new InkCopyPasteService(strokeService),
                    new InkUndoRedoService(inkCanvas, strokeService),
                    new InkFileService(container, inkCanvas, imageContainer, strokeService),
                    new InkZoomService(canvasScroll),
                    new InkPrintService(inkCanvas, container),
                    new InkCloudService());
            };
        }

        private void SetCanvasSize()
        {
            inkCanvas.Width = Math.Max(canvasScroll.ViewportWidth, 1000);
            inkCanvas.Height = Math.Max(canvasScroll.ViewportHeight, 1000);
        }
    }
}
