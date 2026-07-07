using System.Threading;
using System.Threading.Tasks;

using WinMan;

namespace FancyWM.Toasts
{
    internal class ToastService : IToastService
    {
        private readonly IWorkspace m_workspace;
        private readonly object m_toastWindowSync = new();
        private ToastWindow? m_toastWindow;

        public ToastService(IWorkspace workspace)
        {
            m_workspace = workspace;
        }

        private ToastWindow GetOrCreateToastWindow()
        {
            if (m_toastWindow != null)
            {
                return m_toastWindow;
            }

            lock (m_toastWindowSync)
            {
                if (m_toastWindow != null)
                {
                    return m_toastWindow;
                }

                m_toastWindow = App.Current.Dispatcher.Invoke(() => new ToastWindow(m_workspace));
                return m_toastWindow;
            }
        }

        public async Task ShowToastAsync(object content, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var toastWindow = GetOrCreateToastWindow();
            var tcs = new TaskCompletionSource();

            await toastWindow.Dispatcher.InvokeAsync(() => toastWindow.ShowToast(content, cancellationToken));

            using var completeRegistration = cancellationToken.Register(() => tcs.TrySetResult());
            await tcs.Task;
        }
    }
}
