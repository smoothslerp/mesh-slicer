using UnityEngine;
using System.Collections;

public class cpuSideCode : MonoBehaviour {
	static public int kiCalc;							// kernel index, to reference the exact kernel to run and to set buffers for
	// compute buffers are a class to pass information into the compute shader
	static public ComputeBuffer areaRectBuffer;			// this buffer of four double numbers contains rect borders in fractal coordinates
	static public ComputeBuffer colorsBuffer;			// this buffer of 256 colors contains the colors to paint pixels depending on calculations
	// compute buffers can read arrays, but it is not necessary, their size will be passed in the compute shader anyway, to allocate video memory
	static public double[] areaRectArray;				// this array will contain the numbers that will be passet into the buffer
	static public Color[] colorArray;					// this array of COlor will contain the colors, that will be passed to compute shader by compute buffer
	// render texture is a class to have a texture inside video memory and rapidly access it from cpu
	static public RenderTexture outputTexture;			// in this example we use 1024x1024 texture, and Dispatch() method will be called for each pixel
	// now some UI elements
	static public GameObject mainCanvas;				// just Unity canvas
	static public UnityEngine.UI.Image outputImage;		// this image's material will have a reference to the outputTexture, so it will show what we've written there by compute shader
	// this is compute shaders instance
	static public ComputeShader _shader;				// we will need to link the code to this class, and then weĺl use it to actually run kernels from the code
	// the following variables serve input, they hold information about our position in fractal space
	static public double depthFactor;					// how deep we are, zoom factor
	static public double cx, cy;						// center of the view rect
	static public bool move;							// is middle mouse button down?
	static public bool inputChange;						// if any input affected zoom or view position, we need to recalculate and redraw the texture
	void Start(){										// this runs at start and initializes everything
		staticInit();
	}
	static public void staticInit(){
		initControls();									// initializes default values of the input related variables
		initTexture();									// initializes the texture we use to render into
		initCanvas();									// initializes UI elements
		initBuffers();									// initializes arrays, links them to buffers
		initShader();									// we link computer shader code here to the shader class, also buffers are being set for the kernel of compute shader
	}
	static void initControls(){
		depthFactor = 1.0;
		cx = 0;
		cy = 0;
		move = false;
		inputChange = true;
	}
	static void initTexture(){
		outputTexture = new RenderTexture(1024, 1024, 32);
		outputTexture.enableRandomWrite = true;					// this is requred to work as compute shader side written texture
		//outputTexture.memorylessMode = RenderTextureMemoryless.None;
		//outputTexture.
		outputTexture.Create();									// yes, we need to run Create() to actually create the texture
		outputTexture.filterMode = FilterMode.Point;			// not necessary, I just wanted to have clean pixels
	}
	static public void initCanvas(){
		mainCanvas = GameObject.Find("canvas");
		mainCanvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceCamera;
		mainCanvas.GetComponent<Canvas>().worldCamera = Camera.main;
		mainCanvas.GetComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
		mainCanvas.GetComponent<UnityEngine.UI.CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
		mainCanvas.GetComponent<UnityEngine.UI.CanvasScaler>().matchWidthOrHeight = 1.0f;
		outputImage = GameObject.Find("canvas/image").GetComponent<UnityEngine.UI.Image>();
		outputImage.material.mainTexture = outputTexture;
		outputImage.type = UnityEngine.UI.Image.Type.Simple;
		outputImage.GetComponent<RectTransform>().sizeDelta = new Vector2(1080, 1080);
	}
	static void initBuffers(){
		int i;
		// here we define the initial bottom left and top right borders of the fractal space
		areaRectArray = new double[4];
		areaRectArray[0] = -2.0f;
		areaRectArray[1] = -2.0f;
		areaRectArray[2] = 2.0f;
		areaRectArray[3] = 2.0f;
		// now we create and initialize the buffer
		areaRectBuffer = new ComputeBuffer(areaRectArray.Length, sizeof(double));		// amount of video memory for this buffer = array length * element size
		areaRectBuffer.SetData(areaRectArray);		// here we link array to the buffer
		// here we create a color palette to visualize fractal (each pixel will be colored depending on how many iterations required to move a point outside the R = 2 circle)
		colorArray = new Color[256];
		i = 0;
		while (i < colorArray.Length){
			colorArray[i] = new Color(0, 0, 0, 1);
			if (i >= 0 && i < 128)
				colorArray[i] += new Color(0, 0, Mathf.PingPong(i * 4, 256) / 256, 1);
			if (i >= 64 && i < 192)
				colorArray[i] += new Color(0, Mathf.PingPong((i - 64) * 4, 256) / 256, 0, 1);
			if (i >= 128 && i < 256)
				colorArray[i] += new Color(Mathf.PingPong(i * 4, 256) / 256, 0, 0, 1);
			i++;
		}
		colorsBuffer = new ComputeBuffer(colorArray.Length, 4 * 4);		// Color size is four values of four bytes, so 4 * 4
		colorsBuffer.SetData(colorArray);			// again, we're setting color array to the buffer

		/*

		Note: if you want to use compute shader for general purpose calculations, you may want to get the results from the video memory
		Then after dispatch you would need to load data by calling:

		buffer.GetData(someArray)

		this will copy information from gpu side buffer to the cpu side memory
		But getting data from gpu memory is a very slow operation, and may be a bottleneck, so use with caution

		*/
	}
	static void initShader(){
		_shader = Resources.Load<ComputeShader>("csFractal");			// here we link computer shader code file to the shader class
		kiCalc = _shader.FindKernel("pixelCalc");						// we retrieve kernel index by name from the code
		// folowwing three lines allocate video memory and write there our data, kernel will then be able to use the data in calculations
		_shader.SetBuffer(kiCalc, "rect", areaRectBuffer);				// setting rect buffer
		_shader.SetBuffer(kiCalc, "colors", colorsBuffer);				// setting color palette buffer
		_shader.SetTexture(kiCalc, "textureOut", outputTexture);		// setting texture

		/*

		Note:
		One compute shader file can have different kernels. They can share buffers, but you would have to call SetBuffer() for each kernel
		For example:
		_shader.SetBuffer(kernel1, "someBuffer", bufferToShare);
		_shader.SetBuffer(kernel2, "someBuffer", bufferToShare);
		
		*/
	}
	static void calcFractal(){
		_shader.Dispatch(kiCalc, 32, 32, 1);							// Dispatch() method runs its kernel
		/*
			What do the "32, 32, 1" numbers mean? They set the amount of thread groups in X, Y, Z dimensions
			If you open compute shader code of this example, you will see, that our "pixelCalc" kernel has [numthreads(32,32,1)] line,
			and this line means that each group has that much threads per group
			
			The most important thing about these numbers is that kernel has uint3 id : SV_DispatchThreadID parameter
			and this id parameter contains the three dimentional index of the current thread being calculated

			Both Dispatch() and [numthread] have X, Y, Z parameters, and they multiply
			In our case we have (32, 32, 1) groups from Dispatch() and (32, 32, 1) threads per group
			so there will be runned (32 * 32, 32 * 32, 1) = (1024, 1024, 1) threads
			or one thread for each pixel of 1024 x 1024 texture
			we could have Dispatch(kiCalc, 1024, 1024, 1) and [numthreads(1, 1, 1)]
			or Dispatch(kiCalc, 256, 8, 1) and [numthreads(4, 128, 1)]
			it would give us the same (1024, 1024, 1) number of threads
			and inside compute shader we have access to the current thread index through the uint3 id : SV_DispatchThreadID parameter
			In our case we have 1024 threads at X dimension and 1024 threads at Y dimension, so inside compute shader:
			id.x will have values in [0, 1024] range
			id.y will have values in [0, 1024] range
			And therefore we will be able to set the correct pixel's color based on this id parameter

			for more information about thread numbers, check this msdn link:

			https://msdn.microsoft.com/en-us/library/windows/desktop/ff471442(v=vs.85).aspx
			
		*/
	}
	void Update(){
		input();					// cheking player's input
		if (inputChange) {			// if some input came, we need to refresh the image
			calcFractal();			// runs the kernel
			inputChange = false;
		}
	}
	void input(){					// in this method we simply change size and position of the view rect in fractal dimension, but it is always projected to the texture
		double k = 0.0009765625f;
		double borderChange = 2.0;
		if (Input.mouseScrollDelta.y != 0) {
			depthFactor -= 0.2 * depthFactor * Input.mouseScrollDelta.y;
			inputChange = true;
		}
		if (Input.GetButtonDown("Fire1")) {
			move = true;
		}
		if (Input.GetButtonUp("Fire1")) {
			move = false;
		}
		if (move) {
			cx -= 100 * k * depthFactor * Input.GetAxis("Mouse X");
			cy -= 100 * k * depthFactor * Input.GetAxis("Mouse Y");
		}
		if (move && (Input.GetAxis("Mouse X") != 0 || Input.GetAxis("Mouse Y") != 0))
			inputChange = true;
		if (inputChange){
			areaRectArray[0] = cx - depthFactor * borderChange;
			areaRectArray[1] = cy - depthFactor * borderChange;
			areaRectArray[2] = cx + depthFactor * borderChange;
			areaRectArray[3] = cy + depthFactor * borderChange;
			areaRectBuffer.SetData(areaRectArray);					// after changing borders, we again submit the changes by sending them to videomemory
		}
	}
	void OnDestroy(){					// we need to explicitly release the buffers, otherwise Unity will not be satisfied
		areaRectBuffer.Release();
		colorsBuffer.Release();
	}
}
