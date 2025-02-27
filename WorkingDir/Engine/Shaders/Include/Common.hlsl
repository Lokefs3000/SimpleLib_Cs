#ifndef USE_BINDLESS_DESCRIPTORS
#define RESOURCE(type, name, spec) type name : register(spec)
#define GET(name) name
#else
//#define RESOURCE(type, name, spec) type name = ResourceDescriptorHeap[Ref_##name]
#define RESOURCE(type, name, spec)
#define GET(name) ResourceDescriptorHeap[Ref_##name]
#endif