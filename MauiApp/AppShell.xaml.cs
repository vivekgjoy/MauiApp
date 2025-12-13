using MauiApp.Views;

namespace MauiApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Register routes for all non-root pages
            Routing.RegisterRoute(nameof(AddReportPage), typeof(AddReportPage));
            Routing.RegisterRoute(nameof(ReportsHistoryPage), typeof(ReportsHistoryPage));
            Routing.RegisterRoute(nameof(PDFPreviewPage), typeof(PDFPreviewPage));
            Routing.RegisterRoute(nameof(ImageCommentPage), typeof(ImageCommentPage));
            // OLD: Routing.RegisterRoute(nameof(ImageEditPage), typeof(ImageEditPage)); // Replaced by SkiaSharpImageEditorPage
            Routing.RegisterRoute(nameof(ImageCropPage), typeof(ImageCropPage));
            Routing.RegisterRoute(nameof(ImageSourceSelectionPage), typeof(ImageSourceSelectionPage));
        }
    }
}
