#include <onnxruntime_cxx_api.h>
#include <algorithm>
#include <array>
#include <cmath>
#include <complex>
#include <cstring>
#include <filesystem>
#include <memory>
#include <string>
#include <vector>

#ifdef _WIN32
#define API extern "C" __declspec(dllexport)
#else
#define API extern "C" __attribute__((visibility("default")))
#endif

namespace {
constexpr int N = 343980, FFT = 4096, HOP = 1024, FRAMES = 336;
constexpr float PI = 3.14159265358979323846f;
Ort::Env& environment() {static Ort::Env env(ORT_LOGGING_LEVEL_WARNING, "OpenSekaiAudio");return env;}
thread_local std::string error;
struct Model {
    Ort::Session session{nullptr};
    Ort::RunOptions run;
    explicit Model(const char* path, int threads) {
        Ort::SessionOptions opts;
        opts.SetIntraOpNumThreads(std::clamp(threads, 1, 8));
        opts.SetInterOpNumThreads(1);
        opts.DisableMemPattern();
        opts.DisableCpuMemArena();
        opts.SetGraphOptimizationLevel(GraphOptimizationLevel::ORT_ENABLE_EXTENDED);
        session = Ort::Session(environment(), std::filesystem::u8path(path).c_str(), opts);
    }
};
void fft(std::vector<std::complex<float>>& a, bool inverse) {
    for (int i=1,j=0;i<FFT;i++) {
        int bit=FFT>>1;
        for (;j&bit;bit>>=1) j^=bit;
        j^=bit;
        if(i<j) std::swap(a[i],a[j]);
    }
    for (int len=2;len<=FFT;len<<=1) {
        float angle=(inverse?2:-2)*PI/len;
        std::complex<float> step(std::cos(angle),std::sin(angle));
        for(int i=0;i<FFT;i+=len) {
            std::complex<float> w(1,0);
            for(int j=0;j<len/2;j++) {
                auto u=a[i+j],v=a[i+j+len/2]*w;
                a[i+j]=u+v;a[i+j+len/2]=u-v;w*=step;
            }
        }
    }
    if(inverse) for(auto& v:a)v/=FFT;
}
int reflect(int i,int length) {
    while(i<0||i>=length) i=i<0?-i:2*length-2-i;
    return i;
}
std::array<float, FFT> window() {
    std::array<float, FFT> w{};
    for(int i=0;i<FFT;i++)w[i]=.5f-.5f*std::cos(2*PI*i/FFT);
    return w;
}
// Matches normalized=True, periodic Hann, center=True, reflect padding in Torch.
void spectrum(const float* wave,float* spec) {
    const auto w=window();std::vector<std::complex<float>> a(FFT);
    for(int c=0;c<2;c++) for(int t=0;t<FRAMES;t++) {
        for(int i=0;i<FFT;i++)a[i]=wave[c*N+reflect(t*HOP+i-1536,N)]*w[i];
        fft(a,false);
        for(int f=0;f<2048;f++) {
            spec[((c*2)*2048+f)*FRAMES+t]=a[f].real()/64;
            spec[((c*2+1)*2048+f)*FRAMES+t]=a[f].imag()/64;
        }
    }
}
void reconstruct(const float* spec,const float* temporal,float* out) {
    const auto w=window();std::vector<std::complex<float>> a(FFT);
    std::vector<float> denominator(N,0),sum(N,0);
    // Include zeroed boundary frames in the denominator, exactly as torch.istft.
    for(int t=-2;t<FRAMES+2;t++) for(int i=0;i<FFT;i++) {
        int at=t*HOP+i-1536;
        if(at>=0&&at<N)denominator[at]+=w[i]*w[i];
    }
    for(int c=0;c<8;c++) {
        std::fill(sum.begin(),sum.end(),0);
        for(int t=0;t<FRAMES;t++) {
            for(int f=0;f<2048;f++)a[f]={spec[((c*2)*2048+f)*FRAMES+t],spec[((c*2+1)*2048+f)*FRAMES+t]};
            a[2048]=0;
            for(int f=1;f<2048;f++)a[FFT-f]=std::conj(a[f]);
            fft(a,true);
            for(int i=0;i<FFT;i++) {
                int at=t*HOP+i-1536;
                if(at>=0&&at<N)sum[at]+=a[i].real()*64*w[i];
            }
        }
        for(int i=0;i<N;i++)out[c*N+i]=sum[i]/denominator[i]+temporal[c*N+i];
    }
}
}

API int osa_version(){return 1;}
API const char* osa_error(){return error.c_str();}
API void* osa_open(const char* path,int threads) {
    try {error.clear();return new Model(path,threads);}
    catch(const std::exception& e){error=e.what();return nullptr;}
}
API void osa_close(void* handle){delete static_cast<Model*>(handle);}
API void osa_cancel(void* handle){try{static_cast<Model*>(handle)->run.SetTerminate();}catch(...) {}}
API int osa_separate(void* handle,const float* wave,float* output) {
    try {
        auto& m=*static_cast<Model*>(handle);
        std::vector<float> spec(4*2048*FRAMES);
        spectrum(wave,spec.data());
        const std::array<int64_t,3> ws{1,2,N};
        const std::array<int64_t,4> ss{1,4,2048,FRAMES};
        auto mem=Ort::MemoryInfo::CreateCpu(OrtArenaAllocator,OrtMemTypeDefault);
        std::array<Ort::Value,2> inputs{
            Ort::Value::CreateTensor<float>(mem,const_cast<float*>(wave),2*N,ws.data(),ws.size()),
            Ort::Value::CreateTensor<float>(mem,spec.data(),spec.size(),ss.data(),ss.size())};
        const char* names[]={"wave","spec"};const char* outputs[]={"spec_out","wave_out"};
        auto result=m.session.Run(m.run,names,inputs.data(),2,outputs,2);
        if(result[0].GetTensorTypeAndShapeInfo().GetElementCount()!=16*2048*FRAMES ||
           result[1].GetTensorTypeAndShapeInfo().GetElementCount()!=8*N)throw std::runtime_error("Unexpected separator tensor shape");
        reconstruct(result[0].GetTensorData<float>(),result[1].GetTensorData<float>(),output);
        return 0;
    }catch(const std::exception& e){error=e.what();return -1;}
}
API int osa_emissions(void* handle,const float* wave,int samples,float* output,int capacity) {
    try {
        auto& m=*static_cast<Model*>(handle);
        std::array<int64_t,2> dims{1,samples};
        auto mem=Ort::MemoryInfo::CreateCpu(OrtArenaAllocator,OrtMemTypeDefault);
        auto input=Ort::Value::CreateTensor<float>(mem,const_cast<float*>(wave),samples,dims.data(),2);
        const char* names[]={"wave"};const char* outputs[]={"logits"};
        auto result=m.session.Run(m.run,names,&input,1,outputs,1);
        auto shape=result[0].GetTensorTypeAndShapeInfo().GetShape();
        if(shape.size()!=3||shape[0]!=1||shape[2]!=28||shape[1]*28>capacity)throw std::runtime_error("Unexpected alignment tensor shape");
        std::memcpy(output,result[0].GetTensorData<float>(),size_t(shape[1])*28*sizeof(float));
        return int(shape[1]);
    }catch(const std::exception& e){error=e.what();return -1;}
}
// Numerical reference checks use the same DSP entry points as inference.
API void osa_spectrum(const float* wave,float* spec){spectrum(wave,spec);}
API void osa_reconstruct(const float* spec,const float* temporal,float* out){reconstruct(spec,temporal,out);}
