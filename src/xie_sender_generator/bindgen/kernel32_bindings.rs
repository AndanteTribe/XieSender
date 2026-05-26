// bindgen 出力形式 (手書き) — kernel32.dll API 定義
// build.rs の input_bindgen_file で読み込まれる
extern "C" {
    pub fn GetCurrentThread() -> *mut ::std::os::raw::c_void;
    pub fn SetThreadAffinityMask(
        hThread: *mut ::std::os::raw::c_void,
        dwThreadAffinityMask: usize,
    ) -> usize;
}
