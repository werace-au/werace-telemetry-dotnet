#!/bin/bash

# release.sh - A script for managing releases of .NET projects
# This script analyzes conventional commits, updates version numbers, and handles NuGet publishing
# It uses semantic versioning based on conventional commit messages

set -e # Exit on any error

# Default configuration
BUILD_CONFIGURATION="Release"
DRY_RUN=false
NO_GIT=false
FORCE_VERSION_TYPE="patch"
PUBLISH_ONLY=false

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Print a message with color
print_message() {
  local color=$1
  local message=$2
  echo -e "${color}${message}${NC}"
}

# Print an error message and exit
error_exit() {
  print_message "$RED" "Error: $1"
  exit 1
}

# Show help message
show_help() {
  cat <<EOF
Usage: ./release.sh [options]

Options:
    -h, --help             Show this help message
    -d, --dry-run          Show what would happen without making changes
    -c, --configuration    Build configuration (default: Release)
    -k, --nuget-key        NuGet API key (can also be set via NUGET_API_KEY env variable)
    -n, --no-git           Update versions without creating git commits/tags
    -f, --force TYPE       Force a version bump of specified type (patch|feature|major)
                           Defaults to patch if no type specified
    -p, --publish-only     Publish current version to NuGet without making changes

Example:
    ./release.sh --dry-run
    ./release.sh --configuration Debug
    ./release.sh --nuget-key "your-api-key"
    ./release.sh --no-git
    ./release.sh --force patch
    ./release.sh --force feature
    ./release.sh --force major
    ./release.sh --publish-only --nuget-key "your-api-key"
EOF
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
  case $1 in
  -h | --help)
    show_help
    exit 0
    ;;
  -d | --dry-run)
    DRY_RUN=true
    shift
    ;;
  -c | --configuration)
    BUILD_CONFIGURATION="$2"
    shift 2
    ;;
  -k | --nuget-key)
    NUGET_API_KEY="$2"
    shift 2
    ;;
  -n | --no-git)
    NO_GIT=true
    shift
    ;;
  -p | --publish-only)
    PUBLISH_ONLY=true
    shift
    ;;
  -f | --force)
    NO_GIT=true
    if [ -n "$2" ] && [[ "$2" =~ ^(patch|feature|major)$ ]]; then
      FORCE_VERSION_TYPE="$2"
      shift 2
    else
      if [ -n "$2" ]; then
        error_exit "Invalid version type: $2. Must be patch, feature, or major"
      else
        shift # Use default patch if no type specified
      fi
    fi
    ;;
  *)
    error_exit "Unknown option: $1"
    ;;
  esac
done

# Get the current version from Directory.Build.props
get_current_version() {
  local version=""
  if [ -f "Directory.Build.props" ]; then
    version=$(grep -o '<Version>.*</Version>' "Directory.Build.props" | sed 's/<Version>\(.*\)<\/Version>/\1/')
    if [ -n "$version" ]; then
      echo "$version"
      return
    fi
  fi
  echo "0.0.0"
}

# Get commits since the last release tag
get_commits_since_last_release() {
  local last_tag=$(git describe --tags --abbrev=0 --match "v[0-9]*.[0-9]*.[0-9]*" 2>/dev/null || echo "")

  if [ -n "$last_tag" ]; then
    git log "${last_tag}..HEAD" --pretty=format:"%s"
  else
    git log --pretty=format:"%s"
  fi
}

# Determine the next version based on conventional commits or force type
get_next_version() {
  local current_version=$1
  local major minor patch

  IFS='.' read -r major minor patch <<< "$current_version"

  # Ensure we have valid numbers, defaulting to 0 if not set
  major=${major:-0}
  minor=${minor:-0}
  patch=${patch:-0}

  if [ "$NO_GIT" = true ]; then
    case "$FORCE_VERSION_TYPE" in
      "major")
        echo "$((major + 1)).0.0"
        ;;
      "feature")
        echo "${major}.$((minor + 1)).0"
        ;;
      "patch")
        echo "${major}.${minor}.$((patch + 1))"
        ;;
    esac
    return
  fi

  local has_breaking_change=false
  local has_feature=false
  local has_fix=false

  while IFS= read -r commit; do
    if [[ "$commit" =~ "BREAKING CHANGE" ]] || [[ "$commit" =~ "!:" ]]; then
      has_breaking_change=true
      break
    elif [[ "$commit" =~ ^\[feat\] ]]; then
      has_feature=true
    elif [[ "$commit" =~ ^\[fix\] ]]; then
      has_fix=true
    fi
  done <<<"$(get_commits_since_last_release)"

  if [ "$has_breaking_change" = true ]; then
    echo "$((major + 1)).0.0"
  elif [ "$has_feature" = true ]; then
    echo "${major}.$((minor + 1)).0"
  elif [ "$has_fix" = true ]; then
    echo "${major}.${minor}.$((patch + 1))"
  else
    echo "$current_version"
  fi
}

# Update version number in Directory.Build.props
update_version_files() {
  local new_version=$1

  if [ -f "Directory.Build.props" ]; then
    # Update Version tag
    sed -i.bak "s/<Version>.*<\/Version>/<Version>$new_version<\/Version>/" "Directory.Build.props"
    rm "Directory.Build.props.bak"
  else
    print_message "$YELLOW" "Warning: Directory.Build.props file not found"
  fi
}

# Create a new git release
create_git_release() {
  local version=$1
  local release_notes=$(get_commits_since_last_release | sed 's/^/* /')

  git tag -a "v$version" -m "Release v$version

$release_notes"
  git push origin "v$version"
}

# Publish packages to NuGet
publish_nuget_packages() {
  local api_key=$1
  local configuration=$2
  local current_version=$3
  local projects=("WeRace.Telemetry" "WeRace.Telemetry.Generator")
  local publish_failed=false

  # Clean solution first
  print_message "$GREEN" "Cleaning solution..."
  dotnet clean -c "$configuration" --nologo --verbosity quiet || return 1
  rm -rf "$artifacts_dir"

  for project in "${projects[@]}"; do
    if [ -d "$project" ]; then
      print_message "$GREEN" "Building and packing $project..."

      # Clean and build the specific project
      if ! dotnet clean "$project/$project.csproj" -c "$configuration" --nologo --verbosity quiet; then
        print_message "$RED" "Failed to clean $project"
        publish_failed=true
        continue
      fi

      if ! dotnet pack "$project/$project.csproj" -c "$configuration" --nologo --verbosity minimal; then
        print_message "$RED" "Failed to pack $project"
        publish_failed=true
        continue
      fi

      # Find and push the specific package with correct version
      local package_path=$(find "${project}/bin/${configuration}" -name "$project.$current_version.nupkg" -type f)
      if [ -n "$package_path" ]; then
        print_message "$GREEN" "Publishing $project version $current_version to NuGet..."
        if ! dotnet nuget push "$package_path" --api-key "$api_key" --source https://api.nuget.org/v3/index.json --skip-duplicate --no-symbols; then
          print_message "$RED" "Failed to publish $project to NuGet"
          publish_failed=true
        fi
      else
        print_message "$RED" "No package found for $project version $current_version"
        publish_failed=true
      fi
    else
      print_message "$RED" "Project directory $project not found"
      publish_failed=true
    fi
  done

  if [ "$publish_failed" = true ]; then
    return 1
  fi
  return 0
}

# Main script execution starts here
main() {
  # Get current version for publishing
  local current_version=$(get_current_version)

  if [ "$PUBLISH_ONLY" = true ]; then
    if [ -z "$NUGET_API_KEY" ]; then
      error_exit "NuGet API key is required for publishing. Use --nuget-key or set NUGET_API_KEY environment variable."
    fi

    print_message "$GREEN" "Publishing current version $current_version to NuGet..."

    if [ "$DRY_RUN" = true ]; then
      print_message "$YELLOW" "\nDry run completed. Would publish version $current_version to NuGet."
      exit 0
    fi

    if ! publish_nuget_packages "$NUGET_API_KEY" "$BUILD_CONFIGURATION" "$current_version"; then
      error_exit "Failed to publish packages to NuGet"
    fi
    print_message "$GREEN" "\nPublishing of version $current_version completed successfully!"
    exit 0
  fi

  # Only check git repository if we're using git features
  if [ "$NO_GIT" = false ]; then
    if [ ! -d ".git" ]; then
      error_exit "Not a git repository. Please run this script from the root of your git repository."
    fi
  fi

  # Get current state
  local commits=""

  if [ "$NO_GIT" = false ]; then
    commits=$(get_commits_since_last_release)
    if [ -z "$commits" ]; then
      print_message "$YELLOW" "No new commits found since last release. Nothing to do."
      exit 0
    fi
  else
    # When not using git, use a placeholder commit message
    if [ "$FORCE_VERSION_TYPE" = "major" ]; then
      commits="[BREAKING CHANGE] Forced major version update"
    elif [ "$FORCE_VERSION_TYPE" = "feature" ]; then
      commits="[feat] Forced feature version update"
    else
      commits="[fix] Forced patch version update"
    fi
  fi

  # Calculate next version
  local next_version=$(get_next_version "$current_version")

  if [ "$next_version" = "$current_version" ]; then
    print_message "$YELLOW" "No version bump needed based on commit messages."
    exit 0
  fi

  print_message "$GREEN" "Current version: $current_version"
  print_message "$GREEN" "Next version: $next_version"
  if [ "$NO_GIT" = false ]; then
    echo -e "\nCommits to be included:"
    echo "$commits" | sed 's/^/* /'
  fi

  if [ "$DRY_RUN" = true ]; then
    print_message "$YELLOW" "\nDry run completed. No changes made."
    exit 0
  fi

  # Confirm with user
  if [ "$DRY_RUN" = false ]; then
    read -p $'\nDo you want to proceed with the version update? (y/n) ' REPLY
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
      print_message "$YELLOW" "Update cancelled."
      exit 0
    fi
  fi

  # Update version in files
  update_version_files "$next_version"

  if [ "$NO_GIT" = false ]; then
    # Commit version updates
    git add Directory.Build.props
    git commit -m "[chore] bumped version to $next_version"

    # Create and push release tag
    create_git_release "$next_version"
  else
    print_message "$GREEN" "Version files updated without git changes"
  fi

  # Publish to NuGet if API key is provided
  if [ -n "$NUGET_API_KEY" ]; then
    print_message "$GREEN" "\nPublishing packages to NuGet..."
    if ! publish_nuget_packages "$NUGET_API_KEY" "$BUILD_CONFIGURATION" "$next_version"; then
      error_exit "Failed to publish packages to NuGet"
    fi
  else
    print_message "$YELLOW" "\nSkipping NuGet publish - no API key provided"
  fi

  print_message "$GREEN" "\nVersion update to v$next_version completed successfully!"
}

main
