terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }

  backend "azurerm" {
    storage_account_name = "diginsightanalyzerstg00"
    container_name = "tfstates"
    key = ""
    use_azuread_auth = true
  }

  required_version = "~> 1.0"
}

variable "suffix" {
  type = string
}

variable "rg" {
  type = object({
    name = string
  })
}

variable "stg" {
  type = object({
    account_tier             = string
    account_replication_type = string
  })
}

locals {
  location = "switzerlandnorth"
}

provider "azurerm" {
  features {}

  tenant_id           = "16b3c013-d300-468d-ac64-7eda0820b6d3"
  subscription_id     = "e30f5ae8-3ae1-41f1-8ce2-564804131bd6"
  storage_use_azuread = true
}

resource "azurerm_resource_group" "rg" {
  name     = var.rg.name != null ? var.rg.name : "diginsight-analyzer-rg-${var.suffix}"
  location = local.location
}

resource "azurerm_virtual_network" "vnet" {
  name                = "diginsight-analyzer-vnet-${var.suffix}"
  location            = local.location
  resource_group_name = azurerm_resource_group.rg.name
  address_space = [
    "10.0.0.0/16"
  ]
}

resource "azurerm_subnet" "infra" {
  name                            = "infra"
  virtual_network_name            = azurerm_virtual_network.vnet.name
  resource_group_name             = azurerm_resource_group.rg.name
  address_prefixes                = ["10.0.0.0/24"]
  default_outbound_access_enabled = false
}

resource "azurerm_network_security_group" "infra" {
  name                = "${azurerm_virtual_network.vnet.name}-${azurerm_subnet.infra.name}-nsg-${local.location}"
  location            = local.location
  resource_group_name = azurerm_resource_group.rg.name
}

resource "azurerm_subnet_network_security_group_association" "infra" {
  subnet_id                 = azurerm_subnet.infra.id
  network_security_group_id = azurerm_network_security_group.infra.id
}

resource "azurerm_subnet" "dns" {
  name                 = "dns"
  virtual_network_name = azurerm_virtual_network.vnet.name
  resource_group_name  = azurerm_resource_group.rg.name
  address_prefixes = [
    "10.0.1.32/28"
  ]

  delegation {
    name = "Microsoft.Network.dnsResolvers"
    service_delegation {
      name = "Microsoft.Network/dnsResolvers"
      actions = [
        "Microsoft.Network/virtualNetworks/subnets/join/action"
      ]
    }
  }
}

resource "azurerm_network_security_group" "dns" {
  name                = "${azurerm_virtual_network.vnet.name}-${azurerm_subnet.dns.name}-nsg-${local.location}"
  location            = local.location
  resource_group_name = azurerm_resource_group.rg.name
}

resource "azurerm_subnet_network_security_group_association" "dns" {
  subnet_id                 = azurerm_subnet.dns.id
  network_security_group_id = azurerm_network_security_group.dns.id
}

resource "azurerm_storage_account" "stg" {
  name                             = "diginsightanalyzerstg${var.suffix}"
  resource_group_name              = azurerm_resource_group.rg.name
  location                         = local.location
  account_tier                     = var.stg.account_tier
  account_replication_type         = var.stg.account_replication_type
  local_user_enabled               = false
  public_network_access_enabled    = false
  shared_access_key_enabled        = false
  allow_nested_items_to_be_public  = false
  cross_tenant_replication_enabled = false
}
