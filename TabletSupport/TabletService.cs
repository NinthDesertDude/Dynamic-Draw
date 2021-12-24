using System;
using WintabDN;

namespace DynamicDraw.TabletSupport
{
    /// <summary>
    /// Handles connecting to a tablet device and fetching/exposing useful data e.g. position and pressure.
    /// </summary>
    public class TabletService : IDisposable
    {
        private TabletService tabletService;
        private CWintabContext winTabContext;
        private CWintabData winTabData;
        private bool disposedValue;

        /// <summary>No direct instantiation allowed</summary>
        private TabletService() { }

        /// <summary>
        /// Returns the tablet service instance, instantiating it if null.
        /// </summary>
        public static TabletService GetTabletService()
        {
            return new TabletService();
        }

        /// <summary>
        /// Callbacks are fired when UpdateTabletData is called if the tablet context is established.
        /// </summary>
        public event Action<WintabPacket> TabletDataReceived;

        /// <summary>
        /// Tries initializing the tablet reading logic. Returns false on failure.
        /// </summary>
        public bool Start()
        {
            try
            {
                winTabContext = CWintabInfo.GetDefaultSystemContext(ECTXOptionValues.CXO_MESSAGES | ECTXOptionValues.CXO_SYSTEM);
                winTabData = new CWintabData(winTabContext);

                // Failed to get the system context
                if (winTabContext == null)
                {
                    return false;
                }

                winTabContext.Name = "DynamicDraw Tablet Event Data Context";

                WintabAxis tabletX = CWintabInfo.GetTabletAxis(EAxisDimension.AXIS_X);
                WintabAxis tabletY = CWintabInfo.GetTabletAxis(EAxisDimension.AXIS_Y);

                winTabContext.InOrgX = 0;
                winTabContext.InOrgY = 0;
                winTabContext.InExtX = tabletX.axMax;
                winTabContext.InExtY = tabletY.axMax;

                // Tablet origin is usually lower left, invert it to be upper left to match screen coord system.
                winTabContext.OutExtY = -winTabContext.OutExtY;

                bool didOpen = winTabContext.Open();

                winTabData.SetWTPacketEventHandler(UpdateTabletData);

                return true;
            }
            catch (DllNotFoundException)
            {
                // winTab32.dll is missing. Tablet users will have it; non-tablet users don't need it.
                return false;
            }
        }

        /// <summary>
        /// Returns true only if the tablet is ready and actively listened for.
        /// </summary>
        public bool IsTabletPresent()
        {
            return winTabContext != null && CWintabInfo.IsStylusActive();
        }

        /// <summary>
        /// Fires all callbacks attached to the TabletDataReceived event each step that data is received.
        /// </summary>
        private void UpdateTabletData(object emptySender, MessageReceivedEventArgs message)
        {
            if (winTabData == null)
            {
                return;
            }

            try
            {
                uint packetId = (uint)message.Message.WParam;
                WintabPacket packet = winTabData.GetDataPacket(message.Message.LParam, packetId);

                if (packet.pkContext.IsValid)
                {
                    TabletDataReceived?.Invoke(packet);
                }
            }
            catch
            {
                // Ignore, logging would be too frequent.
            }
        }

        #region IDisposable
        ~TabletService()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    winTabData = null;
                }

                tabletService?.Dispose();
                winTabContext?.Close();
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
