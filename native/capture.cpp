#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <d3d11.h>
#include <dxgi1_2.h>
#include <cstring>
#include <cwchar>
#include <new>

template<class T> void release(T*& value){if(value){value->Release();value=nullptr;}}
struct Capture {
 ID3D11Device* device=nullptr;
 ID3D11DeviceContext* context=nullptr;
 IDXGIOutputDuplication* duplication=nullptr;
 ID3D11Texture2D* staging=nullptr;
 UINT width=0,height=0;
 DXGI_MODE_ROTATION rotation=DXGI_MODE_ROTATION_IDENTITY;
 ~Capture(){release(staging);release(duplication);release(context);release(device);}
};
extern "C" __declspec(dllexport) void* ra_open(const wchar_t* deviceName){
 if(!deviceName)return nullptr;
 auto* c=new(std::nothrow) Capture;if(!c)return nullptr;
 IDXGIFactory1* factory=nullptr;
 if(FAILED(CreateDXGIFactory1(__uuidof(IDXGIFactory1),reinterpret_cast<void**>(&factory)))){delete c;return nullptr;}
 bool found=false;
 for(UINT ai=0;!found;++ai){IDXGIAdapter1* adapter=nullptr;if(FAILED(factory->EnumAdapters1(ai,&adapter)))break;
  for(UINT oi=0;!found;++oi){IDXGIOutput* output=nullptr;if(FAILED(adapter->EnumOutputs(oi,&output)))break;
   DXGI_OUTPUT_DESC desc{};output->GetDesc(&desc);
   if(desc.AttachedToDesktop&&std::wcscmp(desc.DeviceName,deviceName)==0){
    D3D_FEATURE_LEVEL level;
    HRESULT hr=D3D11CreateDevice(adapter,D3D_DRIVER_TYPE_UNKNOWN,nullptr,D3D11_CREATE_DEVICE_BGRA_SUPPORT,nullptr,0,D3D11_SDK_VERSION,&c->device,&level,&c->context);
    IDXGIOutput1* output1=nullptr;
    if(SUCCEEDED(hr))hr=output->QueryInterface(__uuidof(IDXGIOutput1),reinterpret_cast<void**>(&output1));
    if(SUCCEEDED(hr))hr=output1->DuplicateOutput(c->device,&c->duplication);
    release(output1);
    if(SUCCEEDED(hr)){c->rotation=desc.Rotation;found=true;}
   }
   release(output);
  }
  release(adapter);
 }
 release(factory);if(!found){delete c;return nullptr;}return c;
}
extern "C" __declspec(dllexport) int ra_frame(void* handle,void* pixels,int capacity,int* width,int* height,int* rotation){
 auto* c=static_cast<Capture*>(handle);if(!c||!pixels)return -1;
 DXGI_OUTDUPL_FRAME_INFO info{};IDXGIResource* resource=nullptr;
 HRESULT hr=c->duplication->AcquireNextFrame(100,&info,&resource);
 if(hr==DXGI_ERROR_WAIT_TIMEOUT)return 0;if(FAILED(hr))return -1;
 ID3D11Texture2D* texture=nullptr;hr=resource->QueryInterface(__uuidof(ID3D11Texture2D),reinterpret_cast<void**>(&texture));release(resource);
 if(FAILED(hr)){c->duplication->ReleaseFrame();return -1;}
 D3D11_TEXTURE2D_DESC desc{};texture->GetDesc(&desc);
 if(desc.Width>7680||desc.Height>4320||static_cast<unsigned long long>(desc.Width)*desc.Height*4>static_cast<unsigned>(capacity)){release(texture);c->duplication->ReleaseFrame();return -1;}
 if(!c->staging||c->width!=desc.Width||c->height!=desc.Height){release(c->staging);desc.Usage=D3D11_USAGE_STAGING;desc.BindFlags=0;desc.CPUAccessFlags=D3D11_CPU_ACCESS_READ;desc.MiscFlags=0;
  hr=c->device->CreateTexture2D(&desc,nullptr,&c->staging);c->width=desc.Width;c->height=desc.Height;}
 if(SUCCEEDED(hr)){c->context->CopyResource(c->staging,texture);D3D11_MAPPED_SUBRESOURCE mapped{};hr=c->context->Map(c->staging,0,D3D11_MAP_READ,0,&mapped);
  if(SUCCEEDED(hr)){for(UINT y=0;y<c->height;y++)std::memcpy(static_cast<char*>(pixels)+y*c->width*4,static_cast<char*>(mapped.pData)+y*mapped.RowPitch,c->width*4);c->context->Unmap(c->staging,0);}}
 release(texture);c->duplication->ReleaseFrame();if(FAILED(hr))return -1;*width=c->width;*height=c->height;*rotation=c->rotation;return 1;
}
extern "C" __declspec(dllexport) void ra_close(void* handle){delete static_cast<Capture*>(handle);}
