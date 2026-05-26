// bindgen 出力形式 (手書き) — XInput1_4.dll API 定義
// build.rs の input_bindgen_file で読み込まれる

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct XINPUT_GAMEPAD {
    pub wButtons: u16,
    pub bLeftTrigger: u8,
    pub bRightTrigger: u8,
    pub sThumbLX: i16,
    pub sThumbLY: i16,
    pub sThumbRX: i16,
    pub sThumbRY: i16,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct XINPUT_STATE {
    pub dwPacketNumber: u32,
    pub Gamepad: XINPUT_GAMEPAD,
}

extern "C" {
    pub fn XInputGetState(dwUserIndex: u32, pState: *mut XINPUT_STATE) -> u32;
}
