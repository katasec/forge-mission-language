# Detect platform RID for single-file publish
UNAME_S := $(shell uname -s)
UNAME_M := $(shell uname -m)

ifeq ($(UNAME_S),Darwin)
  ifeq ($(UNAME_M),arm64)
    RID := osx-arm64
  else
    RID := osx-x64
  endif
else ifeq ($(UNAME_S),Linux)
  ifeq ($(UNAME_M),aarch64)
    RID := linux-arm64
  else
    RID := linux-x64
  endif
endif

INSTALL_DIR := $(HOME)/.local/bin
CLI         := src/ForgeMission.Cli

.PHONY: build test install clean

build:
	dotnet build src/

test:
	dotnet test src/

install:
	dotnet publish $(CLI) \
		-c Release \
		-r $(RID) \
		--self-contained false \
		-p:PublishSingleFile=true \
		-p:DebugType=none \
		-o $(INSTALL_DIR)
	@echo "Installed: $(INSTALL_DIR)/fml"

clean:
	dotnet clean src/
	find src/ -name bin -o -name obj | xargs rm -rf
