fn main() {
    let out_dir = "../XieSender";

    // XInput1_4.dll バインディング生成 (C to C# — Rust DLL 不要)
    csbindgen::Builder::default()
        .input_bindgen_file("bindgen/xinput_bindings.rs")
        .csharp_dll_name("XInput1_4")
        .csharp_namespace("XieSender")
        .csharp_class_name("XInput")
        .generate_csharp_file(&format!("{out_dir}/XInput.g.cs"))
        .unwrap();

    // kernel32.dll バインディング生成 (C to C# — Rust DLL 不要)
    csbindgen::Builder::default()
        .input_bindgen_file("bindgen/kernel32_bindings.rs")
        .csharp_dll_name("kernel32")
        .csharp_namespace("XieSender")
        .csharp_class_name("Kernel32")
        .generate_csharp_file(&format!("{out_dir}/Kernel32.g.cs"))
        .unwrap();
}