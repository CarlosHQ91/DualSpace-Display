using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace VirtualDisplayApp.Rendering
{
    public class D3D11Renderer : IDisposable
    {
        private readonly ID3D11Device _device;
        private readonly ID3D11DeviceContext _context;
        private IDXGISwapChain1? _swapChain;
        private ID3D11RenderTargetView? _renderTargetView;
        private ID3D11VertexShader? _vertexShader;
        private ID3D11PixelShader? _pixelShader;
        private ID3D11InputLayout? _inputLayout;
        private ID3D11Buffer? _vertexBuffer;
        private ID3D11SamplerState? _samplerState;

        private int _width;
        private int _height;

        [StructLayout(LayoutKind.Sequential)]
        private struct Vertex
        {
            public Vector2 Position;
            public Vector2 TexCoord;

            public Vertex(float x, float y, float u, float v)
            {
                Position = new Vector2(x, y);
                TexCoord = new Vector2(u, v);
            }
        }

        public D3D11Renderer(ID3D11Device device, ID3D11DeviceContext context)
        {
            _device = device;
            _context = context;
            InitializeShaders();
        }

        private void InitializeShaders()
        {
            const string hlslSource = """
                struct VS_INPUT {
                    float2 pos : POSITION;
                    float2 uv  : TEXCOORD0;
                };

                struct PS_INPUT {
                    float4 pos : SV_POSITION;
                    float2 uv  : TEXCOORD0;
                };

                PS_INPUT VSMain(VS_INPUT input) {
                    PS_INPUT output;
                    output.pos = float4(input.pos, 0.0f, 1.0f);
                    output.uv = input.uv;
                    return output;
                }

                Texture2D screenTexture : register(t0);
                SamplerState linearSampler : register(s0);

                float4 PSMain(PS_INPUT input) : SV_Target {
                    return screenTexture.Sample(linearSampler, input.uv);
                }
            """;

            // Compile Vertex Shader
            var vsBlob = Compiler.Compile(hlslSource, "VSMain", "shaders.hlsl", "vs_4_0");
            _vertexShader = _device.CreateVertexShader(vsBlob.Span);

            // Input Layout
            var inputElements = new[]
            {
                new InputElementDescription("POSITION", 0, Format.R32G32_Float, 0, 0, InputClassification.PerVertexData, 0),
                new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 8, 0, InputClassification.PerVertexData, 0)
            };
            _inputLayout = _device.CreateInputLayout(inputElements, vsBlob.Span);

            // Compile Pixel Shader
            var psBlob = Compiler.Compile(hlslSource, "PSMain", "shaders.hlsl", "ps_4_0");
            _pixelShader = _device.CreatePixelShader(psBlob.Span);

            // Fullscreen quad vertices (2 triangles)
            var vertices = new[]
            {
                new Vertex(-1.0f,  1.0f, 0.0f, 0.0f), // Top Left
                new Vertex( 1.0f,  1.0f, 1.0f, 0.0f), // Top Right
                new Vertex(-1.0f, -1.0f, 0.0f, 1.0f), // Bottom Left

                new Vertex(-1.0f, -1.0f, 0.0f, 1.0f), // Bottom Left
                new Vertex( 1.0f,  1.0f, 1.0f, 0.0f), // Top Right
                new Vertex( 1.0f, -1.0f, 1.0f, 1.0f)  // Bottom Right
            };

            _vertexBuffer = _device.CreateBuffer(vertices, BindFlags.VertexBuffer);

            // Bilinear Sampler
            var samplerDesc = new SamplerDescription
            {
                Filter = Filter.MinMagMipLinear,
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                ComparisonFunc = ComparisonFunction.Never,
                MinLOD = 0,
                MaxLOD = float.MaxValue
            };
            _samplerState = _device.CreateSamplerState(samplerDesc);
        }

        public void InitializeSwapChain(IntPtr hwnd, int width, int height)
        {
            _width = Math.Max(1, width);
            _height = Math.Max(1, height);

            using var dxgiDevice = _device.QueryInterface<IDXGIDevice>();
            using var adapter = dxgiDevice.GetAdapter();
            using var factory = adapter.GetParent<IDXGIFactory2>();

            var desc = new SwapChainDescription1
            {
                Width = (uint)_width,
                Height = (uint)_height,
                Format = Format.B8G8R8A8_UNorm,
                Stereo = false,
                SampleDescription = new SampleDescription(1, 0),
                BufferUsage = Usage.RenderTargetOutput,
                BufferCount = 2,
                Scaling = Scaling.Stretch,
                SwapEffect = SwapEffect.FlipDiscard,
                AlphaMode = AlphaMode.Ignore,
                Flags = SwapChainFlags.None
            };

            _swapChain = factory.CreateSwapChainForHwnd(_device, hwnd, desc);
            CreateRenderTarget();
        }

        private void CreateRenderTarget()
        {
            if (_swapChain == null) return;
            using var backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
            _renderTargetView = _device.CreateRenderTargetView(backBuffer);
        }

        public void Resize(int width, int height)
        {
            if (_swapChain == null || width <= 0 || height <= 0) return;
            if (_width == width && _height == height) return;

            _width = width;
            _height = height;

            _renderTargetView?.Dispose();
            _renderTargetView = null;

            _swapChain.ResizeBuffers(2, (uint)_width, (uint)_height, Format.B8G8R8A8_UNorm, SwapChainFlags.None);
            CreateRenderTarget();
        }

        public Viewport CurrentViewport { get; private set; }

        public void RenderFrame(ID3D11Texture2D sourceTexture)
        {
            if (_swapChain == null || _renderTargetView == null) return;

            // Calculate aspect ratio preserving viewport
            float targetAspect = (float)sourceTexture.Description.Width / sourceTexture.Description.Height;
            float windowAspect = (float)_width / _height;

            int vpX = 0, vpY = 0, vpW = _width, vpH = _height;
            if (windowAspect > targetAspect)
            {
                vpW = (int)(_height * targetAspect);
                vpX = (_width - vpW) / 2;
            }
            else
            {
                vpH = (int)(_width / targetAspect);
                vpY = (_height - vpH) / 2;
            }

            CurrentViewport = new Viewport(vpX, vpY, vpW, vpH);

            // Create Shader Resource View for the source texture
            var srvDesc = new ShaderResourceViewDescription(
                sourceTexture,
                ShaderResourceViewDimension.Texture2D,
                Format.B8G8R8A8_UNorm,
                0, 1);

            using var srv = _device.CreateShaderResourceView(sourceTexture, srvDesc);

            // Set render target
            _context.OMSetRenderTargets(_renderTargetView);

            // Clear background (black bars)
            _context.ClearRenderTargetView(_renderTargetView, new Color4(0.08f, 0.08f, 0.09f, 1.0f));

            // Setup viewport
            _context.RSSetViewport(CurrentViewport);

            // Bind pipeline states
            _context.IASetInputLayout(_inputLayout);
            _context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            _context.IASetVertexBuffer(0, _vertexBuffer!, (uint)Marshal.SizeOf<Vertex>());

            _context.VSSetShader(_vertexShader);
            _context.PSSetShader(_pixelShader);
            _context.PSSetShaderResource(0, srv);
            _context.PSSetSampler(0, _samplerState);

            // Draw fullscreen quad
            _context.Draw(6, 0);

            // Unbind texture to avoid hazard
            _context.PSSetShaderResource(0, (ID3D11ShaderResourceView?)null!);

            // Present frame immediately (0 for lowest latency / 1 for vsync)
            _swapChain.Present(1, PresentFlags.None);
        }

        public void Dispose()
        {
            _samplerState?.Dispose();
            _vertexBuffer?.Dispose();
            _inputLayout?.Dispose();
            _vertexShader?.Dispose();
            _pixelShader?.Dispose();
            _renderTargetView?.Dispose();
            _swapChain?.Dispose();
        }
    }
}
