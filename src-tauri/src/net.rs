//! Airgap probe, ported from Slip39Demo.Desktop/Services/LinuxConnectivityProbe.cs.
//!
//! Online means any non-loopback interface has a live carrier, read from
//! /sys/class/net/<if>/carrier. Carrier is used rather than operstate because idle
//! NIC drivers commonly report operstate "unknown", which false-positives an
//! airgapped machine as online.
//!
//! Fail-safe direction: if /sys cannot be enumerated at all, or if the enumeration
//! breaks part way through, report online, so generation falls into the INSECURE-TEST
//! path instead of silently passing as airgapped. A single unreadable carrier file is
//! different: the kernel refuses that read for an admin-down interface, which means no
//! link is possible, so that one interface counts as offline.
//!
//! The asymmetry is the whole point. Saying online when the machine is airgapped costs
//! the user a watermark they did not deserve. Saying offline when the machine is
//! connected hands them a backup that presents itself as trustworthy, made from a seed
//! phrase typed on a networked computer. Every unknown resolves toward the first.

use std::path::Path;

pub fn any_carrier_live(net_dir: &Path) -> bool {
    // Mutable because Iterator::any takes &mut self, and this is the iterator itself
    // rather than an adapter over it.
    let mut entries = match std::fs::read_dir(net_dir) {
        Ok(entries) => entries,
        // Cannot enumerate at all: assume the worst.
        Err(_) => return true,
    };

    entries.any(|entry| match entry {
        // An entry the iterator itself could not produce. This is a different thing from
        // an interface whose carrier file will not open: it means the enumeration is
        // incomplete, so an interface may exist that was never examined. Same reasoning
        // as the read_dir failure above, same answer: report online.
        //
        // Untested, deliberately and with the reason stated rather than left as a gap:
        // there is no portable way to make ReadDir yield an Err on demand, and a test
        // that faked the iterator would be testing the fake. The branch is one line and
        // its direction is the safe one, which is the property that matters.
        Err(_) => true,
        Ok(entry) if entry.file_name() == "lo" => false,
        Ok(entry) => matches!(
            std::fs::read_to_string(entry.path().join("carrier")),
            Ok(text) if text.trim() == "1"
        ),
    })
}

#[tauri::command]
pub fn is_online() -> bool {
    any_carrier_live(Path::new("/sys/class/net"))
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::fs;

    fn iface(root: &Path, name: &str, carrier: Option<&str>) {
        let dir = root.join(name);
        fs::create_dir_all(&dir).unwrap();
        if let Some(value) = carrier {
            fs::write(dir.join("carrier"), value).unwrap();
        }
    }

    fn temp(name: &str) -> std::path::PathBuf {
        let dir = std::env::temp_dir().join(format!("slip39-net-{name}"));
        let _ = fs::remove_dir_all(&dir);
        fs::create_dir_all(&dir).unwrap();
        dir
    }

    #[test]
    fn loopback_alone_is_offline() {
        let root = temp("lo-only");
        iface(&root, "lo", Some("1"));
        assert!(!any_carrier_live(&root));
    }

    #[test]
    fn a_live_carrier_is_online() {
        let root = temp("live");
        iface(&root, "lo", Some("1"));
        iface(&root, "eth0", Some("1"));
        assert!(any_carrier_live(&root));
    }

    #[test]
    fn a_down_carrier_is_offline() {
        let root = temp("down");
        iface(&root, "eth0", Some("0"));
        assert!(!any_carrier_live(&root));
    }

    // An admin-down interface makes the kernel refuse the read. No link is possible,
    // so this counts as offline rather than as an unknown.
    #[test]
    fn an_unreadable_carrier_is_offline() {
        let root = temp("unreadable");
        iface(&root, "eth0", None);
        assert!(!any_carrier_live(&root));
    }

    // The whole directory missing is a different thing: the check did not run, so it
    // must report danger.
    #[test]
    fn a_missing_sys_directory_is_online() {
        assert!(any_carrier_live(Path::new("/nonexistent/class/net")));
    }
}
