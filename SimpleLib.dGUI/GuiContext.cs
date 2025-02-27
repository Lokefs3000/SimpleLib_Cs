namespace SimpleLib.dGUI
{
    public class GuiContext : IDisposable
    {
        public GuiContext()
        {
            if (_instance != null)
            {
                throw new InvalidOperationException("A Gui context already exists!");
            }
        }

        public void Dispose()
        {
            if (_instance != this)
            {
                throw new InvalidOperationException("Calling dispose on not active gui context!");
            }



            _instance = null;
        }



        private static GuiContext? _instance = null;
    }
}
