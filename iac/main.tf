terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.0"
    }
  }

  #backend "azurerm" {
  #  storage_account_name = "diginsightanalyzerstg00"
  #  container_name = "tfstates"
  #  key = ""
  #  use_azuread_auth = true
  #}

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
  location  = "switzerlandnorth"
  tenant_id = "16b3c013-d300-468d-ac64-7eda0820b6d3"
}

provider "azurerm" {
  features {}

  tenant_id           = local.tenant_id
  subscription_id     = "e30f5ae8-3ae1-41f1-8ce2-564804131bd6"
  storage_use_azuread = true
}

provider "random" {}

resource "azurerm_resource_group" "main" {
  name     = var.rg.name != null ? var.rg.name : "diginsight-analyzer-rg-${var.suffix}"
  location = local.location
}

resource "azurerm_virtual_network" "main" {
  name                = "diginsight-analyzer-vnet-${var.suffix}"
  location            = local.location
  resource_group_name = azurerm_resource_group.main.name
  address_space = [
    "10.0.0.0/16"
  ]
}

resource "azurerm_subnet" "infra" {
  name                            = "infra"
  virtual_network_name            = azurerm_virtual_network.main.name
  resource_group_name             = azurerm_resource_group.main.name
  address_prefixes                = ["10.0.0.0/24"]
  default_outbound_access_enabled = false
}

resource "azurerm_network_security_group" "infra" {
  name                = "${azurerm_virtual_network.main.name}-${azurerm_subnet.infra.name}-nsg-${local.location}"
  location            = local.location
  resource_group_name = azurerm_resource_group.main.name
}

resource "azurerm_subnet_network_security_group_association" "infra" {
  subnet_id                 = azurerm_subnet.infra.id
  network_security_group_id = azurerm_network_security_group.infra.id
}

resource "azurerm_subnet" "dns" {
  name                 = "dns"
  virtual_network_name = azurerm_virtual_network.main.name
  resource_group_name  = azurerm_resource_group.main.name
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
  name                = "${azurerm_virtual_network.main.name}-${azurerm_subnet.dns.name}-nsg-${local.location}"
  location            = local.location
  resource_group_name = azurerm_resource_group.main.name
}

resource "azurerm_subnet_network_security_group_association" "dns" {
  subnet_id                 = azurerm_subnet.dns.id
  network_security_group_id = azurerm_network_security_group.dns.id
}

resource "azurerm_private_dns_resolver" "main" {
  name                = "diginsight-analyzer-dnspr-${var.suffix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = local.location
  virtual_network_id  = azurerm_virtual_network.main.id
}

resource "azurerm_private_dns_resolver_inbound_endpoint" "main" {
  name                    = azurerm_subnet.dns.name
  location                = local.location
  private_dns_resolver_id = azurerm_private_dns_resolver.main.id

  ip_configurations {
    subnet_id = azurerm_subnet.dns.id
  }
}

resource "azurerm_public_ip" "vngw" {
  name                = "diginsight-analyzer-ip-${var.suffix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = local.location
  allocation_method   = "Static"
  sku                 = "Standard"
}

resource "azurerm_subnet" "vngw" {
  name                 = "GatewaySubnet"
  virtual_network_name = azurerm_virtual_network.main.name
  resource_group_name  = azurerm_resource_group.main.name
  address_prefixes     = ["10.0.1.0/27"]
}

resource "azurerm_virtual_network_gateway" "main" {
  name                = "diginsight-analyzer-vngw-${var.suffix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = local.location
  type                = "Vpn"
  sku                 = "VpnGw1"

  ip_configuration {
    name                 = "default"
    public_ip_address_id = azurerm_public_ip.vngw.id
    subnet_id            = azurerm_subnet.vngw.id
  }

  custom_route {
    address_prefixes = ["10.0.0.0/16"]
  }

  vpn_client_configuration {
    address_space = ["10.1.0.0/29"]
    aad_audience  = "c632b3df-fb67-4d84-bdcf-b95ad541b5c8"
    aad_issuer    = "https://sts.windows.net/${local.tenant_id}/"
    aad_tenant    = "https://login.microsoftonline.com/${local.tenant_id}"
  }
}

resource "azurerm_storage_account" "main" {
  name                             = "diginsightanalyzerstg${var.suffix}"
  resource_group_name              = azurerm_resource_group.main.name
  location                         = local.location
  account_tier                     = var.stg.account_tier
  account_replication_type         = var.stg.account_replication_type
  local_user_enabled               = false
  public_network_access_enabled    = false
  shared_access_key_enabled        = false
  allow_nested_items_to_be_public  = false
  cross_tenant_replication_enabled = false
}

resource "random_uuid" "stg_nic_ipconfig" {
  keepers = {
    suffix = var.suffix
  }
}

locals {
  stg_pve_name     = "diginsight-analyzer-stg-${random_uuid.stg_nic_ipconfig.keepers.suffix}-pve-01"
  stg_pve_nic_name = "${local.stg_pve_name}-nic"
}

resource "azurerm_network_interface" "stg" {
  name                = local.stg_pve_nic_name
  resource_group_name = azurerm_resource_group.main.name
  location            = local.location

  ip_configuration {
    name                          = "privateEndpointIpConfig.${random_uuid.stg_nic_ipconfig.result}"
    private_ip_address_allocation = "Dynamic"
    subnet_id                     = azurerm_subnet.infra.id
  }
}

resource "azurerm_private_dns_zone" "blob" {
  name                = "privatelink.blob.core.windows.net"
  resource_group_name = azurerm_resource_group.main.name
}

resource "azurerm_private_endpoint" "stg" {
  name                          = local.stg_pve_name
  resource_group_name           = azurerm_resource_group.main.name
  location                      = local.location
  subnet_id                     = azurerm_subnet.infra.id
  custom_network_interface_name = local.stg_pve_nic_name

  private_service_connection {
    name                           = local.stg_pve_name
    is_manual_connection           = false
    private_connection_resource_id = azurerm_storage_account.main.id
    subresource_names              = ["blob"]
  }

  private_dns_zone_group {
    name = "default"
    private_dns_zone_ids = [
      azurerm_private_dns_zone.blob.id
    ]
  }
}

#resource "azurerm_storage_container" "analyses" {
#  name = "analyses"
#  storage_account_id = azurerm_storage_account.main.id
#}

#resource "azurerm_storage_container" "plugins" {
#  name = "plugins"
#  storage_account_id = azurerm_storage_account.main.id
#}
