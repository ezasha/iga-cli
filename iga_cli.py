#!/usr/bin/env python3
"""
igArchiveExtractor Python CLI wrapper

Allows using igArchiveExtractor from Python scripts with subprocess calls.
Fast CLI backend without UI overhead.

Usage:
    from iga_cli import IGAExtractor
    
    extractor = IGAExtractor("path/to/CLI.exe")
    extractor.list_files("game.arc")
    extractor.extract_all("game.arc", "output_folder")
    extractor.extract_single("game.arc", 5, "output_folder")
"""

import subprocess
import json
import re
from pathlib import Path
from typing import List, Dict, Optional, Tuple


class IGAExtractor:
    """Python wrapper for igArchiveExtractor CLI"""

    def __init__(self, cli_path: str = None):
        """
        Initialize extractor with path to CLI executable
        
        Args:
            cli_path: Path to CLI.exe (auto-finds if not provided)
        """
        if cli_path is None:
            # Auto-find CLI.exe
            possible_paths = [
                Path.cwd() / "CLI.exe",
                Path.cwd() / "bin" / "Debug" / "net6.0-windows" / "win-x64" / "CLI.exe",
            ]
            for path in possible_paths:
                if path.exists():
                    cli_path = str(path)
                    break
            
            if cli_path is None:
                raise FileNotFoundError("Could not find CLI.exe. Please specify path explicitly.")
        
        self.cli_path = Path(cli_path)
        if not self.cli_path.exists():
            raise FileNotFoundError(f"CLI.exe not found at: {cli_path}")

    def _run_command(self, args: List[str], capture_output: bool = True) -> Tuple[int, str, str]:
        """
        Run CLI command and return exit code, stdout, stderr
        
        Args:
            args: List of command arguments
            capture_output: Capture stdout/stderr
            
        Returns:
            Tuple of (exit_code, stdout, stderr)
        """
        cmd = [str(self.cli_path)] + args
        
        try:
            result = subprocess.run(
                cmd,
                capture_output=capture_output,
                text=True,
                timeout=300  # 5 minute timeout
            )
            return result.returncode, result.stdout, result.stderr
        except subprocess.TimeoutExpired:
            raise TimeoutError(f"Command timed out: {' '.join(cmd)}")
        except Exception as e:
            raise RuntimeError(f"Failed to execute command: {str(e)}")

    def list_files(self, archive_path: str) -> List[Dict[str, any]]:
        """
        List all files in an archive
        
        Args:
            archive_path: Path to archive file
            
        Returns:
            List of dicts with 'index', 'size', 'path'
        """
        exit_code, stdout, stderr = self._run_command(["list", archive_path])
        
        if exit_code != 0:
            raise RuntimeError(f"List command failed: {stderr}")
        
        files = []
        # Parse output: "0001 |     1234567 bytes | path/to/file"
        for line in stdout.split('\n'):
            match = re.match(r'(\d+)\s*\|\s*(\d+)\s*bytes\s*\|\s*(.+)', line)
            if match:
                files.append({
                    'index': int(match.group(1)),
                    'size': int(match.group(2)),
                    'path': match.group(3)
                })
        
        return files

    def extract_all(self, archive_path: str, output_dir: str, verbose: bool = False) -> Dict[str, int]:
        """
        Extract all files from archive
        
        Args:
            archive_path: Path to archive file
            output_dir: Output directory for extracted files
            verbose: Print extraction progress
            
        Returns:
            Dict with 'total', 'success', 'failed' counts
        """
        exit_code, stdout, stderr = self._run_command(["extract", archive_path, output_dir])
        
        if exit_code != 0:
            raise RuntimeError(f"Extract failed: {stderr}")
        
        if verbose:
            print(stdout)
        
        # Count results
        success = len(re.findall(r'\[.*\] Extracted:', stdout))
        failed = len(re.findall(r'\[.*\] ERROR', stdout))
        
        return {
            'total': success + failed,
            'success': success,
            'failed': failed
        }

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
        exit_code, stdout, stderr = self._run_command([
            "extract-single", archive_path, str(file_index), output_dir
        ])
        
        return exit_code == 0

    def export_textures(self, igz_path: str, output_dir: str, verbose: bool = False) -> int:
        """
        Export all textures from IGZ file
        
        Args:
            igz_path: Path to IGZ file
            output_dir: Output directory for textures
            verbose: Print export progress
            
        Returns:
            Number of textures exported
        """
        exit_code, stdout, stderr = self._run_command([
            "export-textures", igz_path, output_dir
        ])
        
        if exit_code != 0:
            raise RuntimeError(f"Texture export failed: {stderr}")
        
        if verbose:
            print(stdout)
        
        # Count exported textures
        count = len(re.findall(r'Exported:', stdout))
        return count


# Example usage
if __name__ == "__main__":
    import sys
    import time
    
    if len(sys.argv) < 2:
        print("Usage: python iga_cli.py <archive_file> [output_dir]")
        sys.exit(1)
    
    archive = sys.argv[1]
    output = sys.argv[2] if len(sys.argv) > 2 else "./extracted"
    
    try:
        extractor = IGAExtractor()
        
        print(f"Listing files in {archive}...")
        files = extractor.list_files(archive)
        print(f"Found {len(files)} files\n")
        
        for f in files[:10]:  # Show first 10
            print(f"  [{f['index']:4d}] {f['size']:10d} bytes | {f['path']}")
        if len(files) > 10:
            print(f"  ... and {len(files) - 10} more files")
        
        print(f"\nExtracting all files to {output}...")
        start = time.time()
        result = extractor.extract_all(archive, output, verbose=False)
        elapsed = time.time() - start
        
        print(f"Extraction complete!")
        print(f"  Success: {result['success']}/{result['total']}")
        print(f"  Failed: {result['failed']}")
        print(f"  Time: {elapsed:.2f}s")
        
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)
