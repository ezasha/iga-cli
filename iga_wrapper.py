#!/usr/bin/env python3
"""
igArchiveExtractor CLI Python Wrapper

Fast extraction of game archives (.arc, .bld, .pak, .iga) for Python scripts.
Uses the precompiled iga-cli.exe for maximum performance.

Installation:
    Build the CLI first: dotnet build CLI.csproj -c Release

Usage:
    from iga_wrapper import IGAExtractor
    
    extractor = IGAExtractor()
    
    # List files
    files = extractor.list_files("game.arc")
    
    # Extract all
    extractor.extract("game.arc", "./output")
    
    # Extract single file
    extractor.extract_single("game.arc", 0, "./output")
"""

import subprocess
import sys
import re
import json
from pathlib import Path
from typing import List, Dict, Optional, Tuple
import tempfile
import os


class IGAExtractor:
    """Python wrapper for iga-cli.exe - fast game archive extractor"""

    def __init__(self, cli_exe: Optional[str] = None, timeout: int = 600):
        """
        Initialize extractor with path to iga-cli.exe
        
        Args:
            cli_exe: Path to iga-cli.exe. Auto-finds if not provided.
            timeout: Command timeout in seconds (default: 10 minutes)
        """
        if cli_exe is None:
            cli_exe = self._find_cli_executable()
        
        self.cli_exe = Path(cli_exe)
        self.timeout = timeout

        if not self.cli_exe.exists():
            raise FileNotFoundError(
                f"iga-cli.exe not found at: {cli_exe}\n"
                f"Please build the CLI first: dotnet build CLI.csproj -c Release"
            )
        
        self.version = self._get_cli_version()

    def _find_cli_executable(self) -> str:
        """Auto-find iga-cli.exe in common build output locations"""
        search_paths = [
            Path.cwd() / "bin" / "Release" / "net6.0" / "win-x64" / "iga-cli.exe",
            Path.cwd() / "bin" / "Debug" / "net6.0" / "win-x64" / "iga-cli.exe",
            Path.cwd() / "iga-cli.exe",
        ]
        
        for path in search_paths:
            if path.exists():
                return str(path)
        
        raise FileNotFoundError(
            "iga-cli.exe not found. Please run: dotnet build CLI.csproj -c Release"
        )

    def _run_command(self, args: List[str], capture_output: bool = True) -> Tuple[int, str, str]:
        """
        Run CLI command and return exit code, stdout, stderr
        
        Args:
            args: Command arguments
            capture_output: Capture stdout/stderr
            
        Returns:
            Tuple of (exit_code, stdout, stderr)
        """
        cmd = [str(self.cli_exe)] + args
        
        try:
            result = subprocess.run(
                cmd,
                capture_output=capture_output,
                text=True,
                timeout=self.timeout
            )
            return result.returncode, result.stdout, result.stderr
        except subprocess.TimeoutExpired:
            raise TimeoutError(f"Command timed out after {self.timeout}s: {' '.join(cmd)}")
        except Exception as e:
            raise RuntimeError(f"Failed to execute: {str(e)}")

    def _get_cli_version(self) -> Optional[str]:
        """Get CLI version"""
        try:
            _, stdout, _ = self._run_command([])
            match = re.search(r'version[\s:]*([0-9.]+)', stdout, re.IGNORECASE)
            return match.group(1) if match else None
        except:
            return None

    def list_files(self, archive_path: str) -> List[Dict[str, any]]:
        """
        List all files in an archive
        
        Args:
            archive_path: Path to archive file (.arc, .bld, .pak, .iga)
            
        Returns:
            List of dicts with 'index', 'size', 'path'
            
        Raises:
            FileNotFoundError: If archive doesn't exist
            RuntimeError: If extraction fails
        """
        if not Path(archive_path).exists():
            raise FileNotFoundError(f"Archive not found: {archive_path}")

        exit_code, stdout, stderr = self._run_command(["list", archive_path])
        
        if exit_code != 0:
            raise RuntimeError(f"List failed: {stderr}")
        
        files = []
        # Parse output: "0001  |     1234567 bytes | path/to/file"
        for line in stdout.split('\n'):
            match = re.match(r'(\d+)\s+(\d+)\s+(.+)', line)
            if match:
                files.append({
                    'index': int(match.group(1)),
                    'size': int(match.group(2)),
                    'path': match.group(3)
                })
        
        return files

    def extract(self, archive_path: str, output_dir: str, verbose: bool = True) -> Dict[str, any]:
        """
        Extract all files from archive
        
        Args:
            archive_path: Path to archive file
            output_dir: Output directory for extracted files
            verbose: Print extraction progress to console
            
        Returns:
            Dict with 'success', 'failed', 'time_elapsed' keys
            
        Raises:
            FileNotFoundError: If archive doesn't exist
            RuntimeError: If extraction fails
        """
        if not Path(archive_path).exists():
            raise FileNotFoundError(f"Archive not found: {archive_path}")

        output_path = Path(output_dir)
        output_path.mkdir(parents=True, exist_ok=True)

        print(f"📦 Extracting: {Path(archive_path).name}")
        print(f"   Output: {output_path.resolve()}")
        print()

        exit_code, stdout, stderr = self._run_command(["extract", archive_path, str(output_dir)])
        
        if verbose:
            # Print progress lines
            for line in stdout.split('\n'):
                if line.strip():
                    print(line)
        
        # Parse result
        success_match = re.search(r'Success:\s*(\d+)', stdout)
        failed_match = re.search(r'Failed:\s*(\d+)', stdout)
        time_match = re.search(r'Time:\s*([\d.]+)s', stdout)
        
        result = {
            'success': int(success_match.group(1)) if success_match else 0,
            'failed': int(failed_match.group(1)) if failed_match else 0,
            'time_elapsed': float(time_match.group(1)) if time_match else 0.0,
            'exit_code': exit_code
        }
        
        if exit_code != 0 and not verbose:
            print(stderr)
        
        return result

    def extract_single(self, archive_path: str, file_index: int, output_dir: str) -> bool:
        """
        Extract a single file by index
        
        Args:
            archive_path: Path to archive file
            file_index: Index of file to extract
            output_dir: Output directory
            
        Returns:
            True if successful, False otherwise
        """
        if not Path(archive_path).exists():
            raise FileNotFoundError(f"Archive not found: {archive_path}")

        output_path = Path(output_dir)
        output_path.mkdir(parents=True, exist_ok=True)

        exit_code, stdout, stderr = self._run_command([
            "extract-single", archive_path, str(file_index), str(output_dir)
        ])
        
        if exit_code == 0:
            print(stdout)
            return True
        else:
            print(stderr, file=sys.stderr)
            return False


# ============================================================================
# Command-line interface
# ============================================================================

def main():
    """Command-line interface for iga-cli"""
    import argparse
    
    parser = argparse.ArgumentParser(
        description="igArchiveExtractor CLI - Fast game archive extraction",
        prog="iga-cli"
    )
    
    subparsers = parser.add_subparsers(dest='command', help='Command to execute')
    
    # List command
    list_parser = subparsers.add_parser('list', help='List files in archive')
    list_parser.add_argument('archive', help='Archive file (.arc, .bld, .pak, .iga)')
    list_parser.add_argument('--format', choices=['text', 'json'], default='text',
                             help='Output format')
    
    # Extract command
    extract_parser = subparsers.add_parser('extract', help='Extract all files')
    extract_parser.add_argument('archive', help='Archive file')
    extract_parser.add_argument('output', help='Output directory')
    extract_parser.add_argument('-q', '--quiet', action='store_true',
                                help='Suppress progress output')
    
    # Extract-single command
    single_parser = subparsers.add_parser('extract-single', help='Extract single file')
    single_parser.add_argument('archive', help='Archive file')
    single_parser.add_argument('index', type=int, help='File index')
    single_parser.add_argument('output', help='Output directory')
    
    args = parser.parse_args()
    
    if not args.command:
        parser.print_help()
        return 1
    
    try:
        extractor = IGAExtractor()
        
        if args.command == 'list':
            files = extractor.list_files(args.archive)
            
            if args.format == 'json':
                print(json.dumps(files, indent=2))
            else:
                print(f"Files: {len(files)}")
                print("="*60)
                for f in files:
                    print(f"{f['index']:4d} {f['size']:15d} {f['path']}")
        
        elif args.command == 'extract':
            result = extractor.extract(args.archive, args.output, verbose=not args.quiet)
            
            if result['exit_code'] != 0:
                return 1
        
        elif args.command == 'extract-single':
            success = extractor.extract_single(args.archive, args.index, args.output)
            return 0 if success else 1
        
        return 0
    
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        return 1


if __name__ == '__main__':
    sys.exit(main())
