# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

## [2.0.0]

### Added
- Windows Forms GUI with dark/light mode toggle
- 4-station live dashboard (Overview + per-station detail tabs)
- Embedded ASP.NET Core web dashboard server for remote monitoring
- Simulation mode for testing without hardware
- Battery profile manager with persistent C-code test parameter profiles
- CSV output option alongside raw line logging
- Auto-reconnect with configurable delay
- CLI flags: `--list-ports`, `--help`, pre-populate GUI connection fields
- Diagnostic analyzer for failure root-cause detection

### Changed
- Complete rewrite; serial logger with WinForms UI replaces the v1.0.0 placeholder

## [1.0.0]

### Added
- Initial project scaffold
