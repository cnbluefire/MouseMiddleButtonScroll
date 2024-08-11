using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using System;
using WinRT;

namespace MouseMiddleButtonScroll.WinUI3.Helpers
{
    internal static class UIElementCursorHelper
    {
        public static InputCursor GetCursor(UIElement element)
        {
            using var reference = ((IWinRTObject)element).NativeObject.As(new Guid("8f69b9e9-1f00-5834-9bf1-a9257bed39f0"));
            return get_ProtectedCursor(reference);
        }

        public static void SetCursor(UIElement element, InputCursor value)
        {
            using var reference = ((IWinRTObject)element).NativeObject.As(new Guid("8f69b9e9-1f00-5834-9bf1-a9257bed39f0"));
            set_ProtectedCursor(reference, value);
        }

        private unsafe static InputCursor get_ProtectedCursor(IObjectReference _obj)
        {
            nint thisPtr = _obj.ThisPtr;
            nint intPtr = default;
            try
            {
                ExceptionHelpers.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, out nint, int>)(*(nint*)(*(nint*)(void*)thisPtr + 6 * (nint)sizeof(delegate* unmanaged[Stdcall]<nint, out nint, int>))))(thisPtr, out intPtr));
                return ABI.Microsoft.UI.Input.InputCursor.FromAbi(intPtr);
            }
            finally
            {
                ABI.Microsoft.UI.Input.InputCursor.DisposeAbi(intPtr);
            }
        }

        private unsafe static void set_ProtectedCursor(IObjectReference _obj, InputCursor value)
        {
            nint thisPtr = _obj.ThisPtr;
            ObjectReferenceValue value2 = default;
            try
            {
                value2 = ABI.Microsoft.UI.Input.InputCursor.CreateMarshaler2(value);
                ExceptionHelpers.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, nint, int>)(*(nint*)(*(nint*)(void*)thisPtr + 7 * (nint)sizeof(delegate* unmanaged[Stdcall]<nint, nint, int>))))(thisPtr, MarshalInspectable<object>.GetAbi(value2)));
            }
            finally
            {
                MarshalInspectable<object>.DisposeMarshaler(value2);
            }
        }
    }
}
